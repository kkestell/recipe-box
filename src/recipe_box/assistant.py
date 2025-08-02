from __future__ import annotations

import asyncio
from dataclasses import replace

import instructor
from pydantic import BaseModel
from PySide6.QtCore import Signal, QSize
from PySide6.QtWidgets import (
    QDialog,
    QVBoxLayout,
    QDialogButtonBox,
    QHBoxLayout,
    QTextEdit,
    QPushButton,
    QSizePolicy,
    QMessageBox,
    QComboBox,
    QLabel,
)

from src.recipe_box.theme import MARGIN
from src.recipe_box.models import Recipe, Step, Component
from src.recipe_box.diff import DiffViewer
from src.recipe_box.preferences import Preferences


class PydanticStep(BaseModel):
    text: str
    ingredients: list[str]


class PydanticComponent(BaseModel):
    name: str | None
    steps: list[PydanticStep]


class PydanticRecipe(BaseModel):
    title: str
    components: list[PydanticComponent]


def prompt_assistant(recipe: Recipe, prompt: str, model: str, api_key: str) -> Recipe:
    client = instructor.from_provider(model, api_key=api_key)

    pydantic_components = []
    for component in recipe.components:
        pydantic_steps = [
            PydanticStep(text=step.text, ingredients=step.ingredients or [])
            for step in component.steps
        ]
        pydantic_components.append(
            PydanticComponent(name=component.name, steps=pydantic_steps)
        )

    pydantic_recipe = PydanticRecipe(title=recipe.title, components=pydantic_components)

    user_message = f"""
    You are a recipe editor. Modify this recipe based on the request below. Only make the specific changes requested - do not alter anything else.

    Request: {prompt}

    Recipe:
    {pydantic_recipe.model_dump_json(indent=2)}
    """

    updated_pydantic_recipe = client.chat.completions.create(
        response_model=PydanticRecipe,
        messages=[{"role": "user", "content": user_message}],
    )

    new_title = updated_pydantic_recipe.title
    new_components = []
    for p_component in updated_pydantic_recipe.components:
        new_steps = [
            Step(text=p_step.text, ingredients=p_step.ingredients or None)
            for p_step in p_component.steps
        ]
        new_components.append(Component(name=p_component.name, steps=new_steps))

    return replace(recipe, title=new_title, components=new_components)


class AssistantDialog(QDialog):
    accepted_with_text = Signal(str)

    def __init__(self, original_text: str, parent=None):
        super().__init__(parent)
        self.setWindowTitle("AI Assistant")
        self.resize(QSize(800, 600))

        self.original_text = original_text
        self.modified_text = self.original_text
        self.prefs = Preferences.instance()

        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(MARGIN, MARGIN, MARGIN, MARGIN)
        main_layout.setSpacing(MARGIN)

        prompt_layout = QHBoxLayout()
        self.prompt_input = QTextEdit()
        self.prompt_input.setPlaceholderText(
            "Enter your instructions, e.g., 'make it vegan' or 'double the ingredients'."
        )
        self.prompt_input.setFixedHeight(60)
        self.update_button = QPushButton("Update")
        self.update_button.setSizePolicy(
            QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Preferred
        )
        prompt_layout.addWidget(self.prompt_input)
        prompt_layout.addWidget(self.update_button)
        main_layout.addLayout(prompt_layout)

        self.diff_viewer = DiffViewer(self.original_text, self.modified_text)
        main_layout.addWidget(self.diff_viewer)

        bottom_layout = QHBoxLayout()
        bottom_layout.addWidget(QLabel("Provider:"))
        self.provider_combo = QComboBox()
        for provider in self.prefs.data.ai_providers:
            if provider.model:
                self.provider_combo.addItem(provider.model, userData=provider)
        bottom_layout.addWidget(self.provider_combo)
        bottom_layout.addStretch()

        self.button_box = QDialogButtonBox(
            QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel
        )
        bottom_layout.addWidget(self.button_box)
        main_layout.addLayout(bottom_layout)

        self.update_button.clicked.connect(self.run_assistant_update)
        self.button_box.accepted.connect(self.accept_and_emit)
        self.button_box.rejected.connect(self.reject)

    def run_assistant_update(self):
        asyncio.create_task(self._run_assistant_update_async())

    async def _run_assistant_update_async(self):
        user_prompt = self.prompt_input.toPlainText().strip()
        if not user_prompt:
            return

        if self.provider_combo.currentIndex() == -1:
            QMessageBox.warning(
                self,
                "Provider Not Selected",
                "No AI provider is selected. Please add or select one in Edit -> Preferences.",
            )
            return

        provider = self.provider_combo.currentData()
        model = provider.model
        api_key = provider.api_key

        if not model or not api_key:
            QMessageBox.warning(
                self,
                "Provider Not Configured",
                "The selected provider is missing a model or API key. Please check your preferences.",
            )
            return

        self._set_controls_enabled(False)
        self.update_button.setText("Updating...")

        try:
            recipe = Recipe.parse(self.original_text)
            modified_recipe = await asyncio.to_thread(
                prompt_assistant, recipe, user_prompt, model, api_key
            )
            self.modified_text = modified_recipe.serialize()
            self.diff_viewer.set_texts(self.original_text, self.modified_text)
        except Exception as e:
            QMessageBox.critical(self, "Error", f"An unexpected error occurred:\n{e}")
            print(f"An error occurred: {e}")
            self.modified_text = self.original_text
            self.diff_viewer.set_texts(self.original_text, self.original_text)
        finally:
            self._set_controls_enabled(True)
            self.update_button.setText("Update")

    def _set_controls_enabled(self, enabled: bool):
        self.prompt_input.setEnabled(enabled)
        self.update_button.setEnabled(enabled)
        self.diff_viewer.setEnabled(enabled)
        self.button_box.setEnabled(enabled)
        self.provider_combo.setEnabled(enabled)

    def accept_and_emit(self):
        self.accepted_with_text.emit(self.modified_text)
        self.accept()
