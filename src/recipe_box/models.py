from __future__ import annotations

import re
from dataclasses import dataclass, field


@dataclass(frozen=True)
class Step:
    text: str
    ingredients: list[str] | None = None


@dataclass(frozen=True)
class Component:
    name: str | None = None
    steps: list[Step] = field(default_factory=list)


@dataclass(frozen=True)
class Recipe:
    id: int | None = None
    title: str = "Untitled"
    metadata: dict[str, str] = field(default_factory=dict)
    components: list[Component] = field(default_factory=list)

    @property
    def draft(self) -> str | None:
        return self.metadata.get("draft")

    @property
    def favorite(self) -> str | None:
        return self.metadata.get("favorite")

    @property
    def notes(self) -> str | None:
        return self.metadata.get("notes")

    @property
    def prep_time(self) -> int | None:
        val = self.metadata.get("prep_time")
        return int(val) if val and val.isdigit() else None

    @property
    def cook_time(self) -> int | None:
        val = self.metadata.get("cook_time")
        return int(val) if val and val.isdigit() else None

    @property
    def yields(self) -> str | None:
        return self.metadata.get("yields")

    @property
    def category(self) -> str:
        return self.metadata.get("category", "Uncategorized")

    @property
    def cuisine(self) -> str | None:
        return self.metadata.get("cuisine")

    @property
    def source(self) -> str | None:
        return self.metadata.get("source")

    @property
    def content(self) -> str:
        return self.serialize()

    @classmethod
    def parse(cls, recipe_text: str) -> Recipe:
        if not recipe_text.strip():
            raise ValueError("Cannot parse an empty recipe.")

        lines = recipe_text.strip().split("\n")
        metadata = {}
        content_lines = lines
        recipe_title = None

        if lines and lines[0].strip() == "---":
            try:
                end_meta_index = lines[1:].index("---") + 1
                meta_lines = lines[1:end_meta_index]
                content_lines = lines[end_meta_index + 1 :]

                for line in meta_lines:
                    if ":" in line:
                        key, value = line.split(":", 1)
                        metadata[key.strip()] = value.strip()
            except ValueError:
                pass

        recipe_notes = []
        final_components = []
        current_component_name: str | None = None
        current_component_steps: list[Step] = []
        current_step_text: str | None = None
        current_step_ingredients: list[str] = []

        def finalize_step():
            nonlocal current_step_text
            if current_step_text:
                step = Step(
                    text=current_step_text,
                    ingredients=(
                        list(current_step_ingredients)
                        if current_step_ingredients
                        else None
                    ),
                )
                current_component_steps.append(step)
            current_step_text = None
            current_step_ingredients.clear()

        def finalize_component():
            nonlocal current_component_name
            finalize_step()
            if current_component_steps:
                component = Component(
                    name=current_component_name, steps=list(current_component_steps)
                )
                final_components.append(component)
            current_component_name = None
            current_component_steps.clear()

        for line in content_lines:
            line = line.lstrip()
            if not line:
                continue

            match = re.match(r"([=>+#-])\s*(.*)", line)
            if not match:
                continue

            prefix, content = match.groups()
            content = content.strip()

            match prefix:
                case "=":
                    recipe_title = content
                case ">":
                    recipe_notes.append(content)
                case "+":
                    finalize_component()
                    current_component_name = content
                case "#":
                    finalize_step()
                    current_step_text = content
                case "-":
                    if current_step_text is None:
                        raise ValueError("Ingredients must belong to a step.")
                    current_step_ingredients.append(content)

        finalize_component()

        if recipe_notes:
            metadata["notes"] = "\n".join(recipe_notes)

        if not recipe_title:
            raise ValueError("No recipe title.")

        if not final_components:
            raise ValueError("Recipe content is missing or invalid.")

        return cls(
            title=recipe_title,
            metadata=metadata,
            components=final_components,
        )

    def serialize(self) -> str:
        lines = []
        metadata = self.metadata.copy()
        notes = metadata.pop("notes", None)

        if (
            "category" in metadata
            and metadata["category"] == "Uncategorized"
            and len(metadata) == 1
        ):
            metadata.pop("category")

        if metadata:
            lines.append("---")
            sorted_items = sorted(metadata.items())
            max_key_length = max(
                len(key) for key, value in sorted_items if value is not None
            )

            for key, value in sorted_items:
                if value is not None:
                    padding = " " * (max_key_length - len(key))
                    lines.append(f"{key}:{padding} {value}")
            lines.append("---")
            lines.append("")

        lines.append(f"= {self.title}")

        if notes:
            lines.append("")
            lines.extend(f"> {line.strip()}" for line in notes.strip().split("\n"))

        for i, component in enumerate(self.components):
            if lines and lines[-1] != "":
                lines.append("")

            if component.name:
                lines.append(f"+ {component.name}")

            if component.steps:
                if component.name:
                    lines.append("")

                for j, step in enumerate(component.steps):
                    lines.append(f"# {step.text}")
                    if step.ingredients:
                        lines.append("")
                        lines.extend([f"- {ing}" for ing in step.ingredients])

                    if j < len(component.steps) - 1:
                        lines.append("")

        return "\n".join(lines) + "\n"
