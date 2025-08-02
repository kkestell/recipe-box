from __future__ import annotations

import asyncio
import os
import subprocess
import sys
import tempfile
from dataclasses import replace
from pathlib import Path

import PySide6.QtAsyncio as QtAsyncio
from PySide6.QtCore import Qt, QSize
from PySide6.QtGui import QAction, QKeySequence, QFont
from PySide6.QtWidgets import (
    QApplication,
    QFileDialog,
    QInputDialog,
    QMainWindow,
    QMessageBox,
    QSplitter,
)

from src.recipe_box.assistant import AssistantDialog
from src.recipe_box.browser import RecipeBrowser
from src.recipe_box.editor import RecipeEditor
from src.recipe_box.jsonld import recipe_from_url
from src.recipe_box.theme import MARGIN
from src.recipe_box.library import Library
from src.recipe_box.models import Recipe
from src.recipe_box.preferences import Preferences, PreferencesDialog
from src.recipe_box.rendering import TypstRenderer


def get_db_path() -> Path:
    if os.name == "nt":
        appdata = os.getenv("APPDATA")
        if appdata:
            return Path(appdata) / "RecipeBox" / "RecipeBox.db"
    return Path.home() / ".config" / "RecipeBox" / "RecipeBox.db"


class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Recipe Box")
        self.resize(QSize(1024, 768))

        self.preferences = Preferences.instance()
        self.original_font = QApplication.instance().font()
        self.preferences.preferencesChanged.connect(self.apply_app_styles)

        self.current_recipe_id: int | None = None
        self.is_editor_dirty: bool = False
        self.lib = Library(get_db_path())

        self.save_action: QAction | None = None
        self.delete_action: QAction | None = None
        self.assistant_action: QAction | None = None
        self.export_recipe_action: QAction | None = None
        self.export_cookbook_action: QAction | None = None

        self.splitter = QSplitter(self)
        self.setCentralWidget(self.splitter)
        self.recipe_browser = RecipeBrowser()
        self.recipe_editor = RecipeEditor()
        self.splitter.addWidget(self.recipe_browser)
        self.splitter.addWidget(self.recipe_editor)
        self.splitter.setSizes([300, 700])
        self.splitter.setHandleWidth(MARGIN)
        self.setStatusBar(self.statusBar())

        self.recipe_browser.recipeSelected.connect(self.display_recipe)
        self.recipe_editor.dirtyStateChanged.connect(self.set_dirty)

        self.setup_menu()
        self.load_recipes()
        self.recipe_editor.setEnabled(False)
        self.update_action_states()
        self.apply_app_styles()

    def setup_menu(self):
        menu_bar = self.menuBar()

        file_menu = menu_bar.addMenu("&File")
        new_action = QAction("&New", self)
        new_action.setShortcut(QKeySequence.StandardKey.New)
        new_action.triggered.connect(self.new_recipe)
        file_menu.addAction(new_action)

        self.save_action = QAction("&Save", self)
        self.save_action.setShortcut(QKeySequence.StandardKey.Save)
        self.save_action.triggered.connect(self.save_current_recipe)
        file_menu.addAction(self.save_action)

        self.delete_action = QAction("&Delete", self)
        self.delete_action.setShortcut(QKeySequence.StandardKey.Delete)
        self.delete_action.triggered.connect(self.delete_current_recipe)
        file_menu.addAction(self.delete_action)

        file_menu.addSeparator()
        import_action = QAction("&Import...", self)
        import_action.triggered.connect(
            lambda: asyncio.ensure_future(self.import_from_url())
        )
        file_menu.addAction(import_action)

        self.export_recipe_action = QAction("Export Recipe as PDF...", self)
        self.export_recipe_action.triggered.connect(
            lambda: asyncio.ensure_future(self.export_recipe_as_pdf())
        )
        file_menu.addAction(self.export_recipe_action)

        self.export_cookbook_action = QAction("Export Cookbook...", self)
        self.export_cookbook_action.triggered.connect(
            lambda: asyncio.ensure_future(self.export_cookbook())
        )
        file_menu.addAction(self.export_cookbook_action)

        file_menu.addSeparator()
        exit_action = QAction("E&xit", self)
        exit_action.triggered.connect(self.close)
        file_menu.addAction(exit_action)

        edit_menu = menu_bar.addMenu("&Edit")
        prefs_action = QAction("&Preferences...", self)
        prefs_action.triggered.connect(self.open_preferences_dialog)
        edit_menu.addAction(prefs_action)

        recipe_menu = menu_bar.addMenu("&Recipe")
        self.assistant_action = QAction("AI Assistant...", self)
        self.assistant_action.triggered.connect(self.open_assistant)
        recipe_menu.addAction(self.assistant_action)

        help_menu = menu_bar.addMenu("&Help")
        about_action = QAction("&About", self)
        about_action.triggered.connect(self.show_about_dialog)
        help_menu.addAction(about_action)

    def show_about_dialog(self):
        QMessageBox.about(self, "About Recipe Box", "Recipe Box v1.0.0")

    def open_preferences_dialog(self):
        dialog = PreferencesDialog(self)
        dialog.exec()

    def apply_app_styles(self):
        """Applies application-wide styles from preferences."""
        app = QApplication.instance()
        prefs = self.preferences.data

        font = QFont(self.original_font)

        if prefs.ui.font_family:
            font.setFamily(prefs.ui.font_family)

        if prefs.ui.font_size:
            font.setPointSize(prefs.ui.font_size)

        app.setFont(font)

    def update_action_states(self):
        self.save_action.setEnabled(self.is_editor_dirty)
        has_selection = self.current_recipe_id is not None
        self.delete_action.setEnabled(has_selection)
        self.assistant_action.setEnabled(has_selection)
        self.export_recipe_action.setEnabled(has_selection)
        self.export_cookbook_action.setEnabled(bool(self.lib.list_recipes()))

    async def import_from_url(self):
        url, ok = QInputDialog.getText(self, "Import Recipe", "Enter URL:")
        if not (ok and url):
            return

        if not self._prompt_save_if_dirty():
            return

        self.statusBar().showMessage(f"Importing from {url}...")
        QApplication.setOverrideCursor(Qt.WaitCursor)
        try:
            imported_recipe = recipe_from_url(url)
            self.recipe_browser.select_recipe(None)
            self.current_recipe_id = None
            self.setWindowTitle(f"{imported_recipe.title} - Recipe Box")
            self.recipe_editor.setEnabled(True)
            self.recipe_editor.setFocus()
            self.recipe_editor.set_content(imported_recipe.serialize())
            self.set_dirty(True)
            self.statusBar().showMessage(
                f"Successfully imported '{imported_recipe.title}'. Ready to save.", 5000
            )
        except Exception as e:
            self.statusBar().showMessage("Import failed.", 5000)
            QMessageBox.critical(
                self, "Import Error", f"Failed to import recipe from URL:\n\n{e}"
            )
        finally:
            QApplication.restoreOverrideCursor()

    def set_dirty(self, is_dirty: bool):
        self.is_editor_dirty = is_dirty
        title = self.windowTitle().strip("* ")
        if is_dirty:
            self.setWindowTitle(f"* {title}")
        else:
            self.setWindowTitle(title)
        self.update_action_states()

    def _prompt_save_if_dirty(self) -> bool:
        if not self.is_editor_dirty:
            return True
        reply = QMessageBox.question(
            self,
            "Unsaved Changes",
            "You have unsaved changes. Do you want to save them?",
            QMessageBox.StandardButton.Save
            | QMessageBox.StandardButton.Discard
            | QMessageBox.StandardButton.Cancel,
        )
        if reply == QMessageBox.StandardButton.Save:
            self.save_current_recipe()
            return not self.is_editor_dirty
        elif reply == QMessageBox.StandardButton.Cancel:
            return False
        return True

    def new_recipe(self):
        if not self._prompt_save_if_dirty():
            return
        self.recipe_browser.select_recipe(None)
        self.current_recipe_id = None
        self.setWindowTitle("Untitled - Recipe Box")
        self.recipe_editor.set_content(Recipe().serialize())
        self.recipe_editor.setEnabled(True)
        self.recipe_editor.setFocus()
        self.update_action_states()

    def save_current_recipe(self):
        if self.current_recipe_id is None and not self.is_editor_dirty:
            self.statusBar().showMessage("Nothing to save.", 2000)
            return

        new_content = self.recipe_editor.toPlainText()
        try:
            parsed_recipe = Recipe.parse(new_content)

            if self.current_recipe_id is None:
                new_id = self.lib.add_recipe(parsed_recipe)
                new_recipe = replace(parsed_recipe, id=new_id)
                self.current_recipe_id = new_id
            else:
                new_recipe = replace(parsed_recipe, id=self.current_recipe_id)
                self.lib.update_recipe(new_recipe)

            self.load_recipes()
            self.recipe_browser.select_recipe(self.current_recipe_id)
            self.statusBar().showMessage(f"Saved '{new_recipe.title}'", 3000)
            self.recipe_editor.set_content(new_recipe.serialize())
            self.setWindowTitle(f"{new_recipe.title} - Recipe Box")

        except ValueError as e:
            self.statusBar().showMessage("Error: Invalid recipe format.", 5000)
            QMessageBox.critical(
                self, "Save Error", f"The recipe text could not be parsed.\n\n{e}"
            )

    def delete_current_recipe(self):
        if self.current_recipe_id is None:
            self.statusBar().showMessage("No recipe selected to delete.", 2000)
            return

        recipe = self.lib.get_recipe(self.current_recipe_id)
        if not recipe:
            self.statusBar().showMessage("Recipe not found.", 2000)
            self.load_recipes()
            return

        reply = QMessageBox.question(
            self,
            "Delete Recipe",
            f"Are you sure you want to delete '{recipe.title}'?",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
        )

        if reply == QMessageBox.StandardButton.Yes:
            self.lib.delete_recipe(self.current_recipe_id)
            self.current_recipe_id = None
            self.recipe_editor.set_content("")
            self.recipe_editor.setEnabled(False)
            self.setWindowTitle("Recipe Box")
            self.load_recipes()
            self.statusBar().showMessage(f"Deleted '{recipe.title}'", 3000)
            self.update_action_states()

    def load_recipes(self):
        recipes = self.lib.list_recipes()
        self.recipe_browser.populate(recipes)
        self.update_action_states()

    def display_recipe(self, recipe_id: int | None):
        if recipe_id == self.current_recipe_id:
            return

        if not self._prompt_save_if_dirty():
            self.recipe_browser.select_recipe(self.current_recipe_id)
            return

        self.current_recipe_id = recipe_id
        if recipe_id is None:
            self.setWindowTitle("Recipe Box")
            self.recipe_editor.set_content("")
            self.recipe_editor.setEnabled(False)
        else:
            recipe = self.lib.get_recipe(recipe_id)
            if recipe:
                self.setWindowTitle(f"{recipe.title} - Recipe Box")
                self.recipe_editor.set_content(recipe.serialize())
                self.recipe_editor.setEnabled(True)
            else:
                QMessageBox.warning(
                    self, "Not Found", "The selected recipe could not be found."
                )
                self.setWindowTitle("Recipe Box")
                self.recipe_editor.set_content("")
                self.current_recipe_id = None
                self.recipe_editor.setEnabled(False)
                self.load_recipes()
        self.update_action_states()

    def open_assistant(self):
        if self.current_recipe_id is None:
            self.statusBar().showMessage("Please select a recipe first.", 2000)
            return

        original_text = self.recipe_editor.toPlainText()
        dialog = AssistantDialog(original_text, self)
        dialog.accepted_with_text.connect(self.recipe_editor.setPlainText)
        dialog.exec()

    def _run_typst_in_thread(self, typst_source):
        try:
            with tempfile.TemporaryDirectory() as tempdir:
                source_path = Path(tempdir) / "recipe.typ"
                pdf_path = Path(tempdir) / "recipe.pdf"

                with open(source_path, "w", encoding="utf-8") as f:
                    f.write(typst_source)

                # TODO: Add preference for Typst path
                command = [
                    "typst",
                    "compile",
                    str(source_path),
                    str(pdf_path),
                ]
                result = subprocess.run(
                    command,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    check=False,
                )

                if result.returncode != 0:
                    return None, f"Typst error: {result.stderr}"

                with open(pdf_path, "rb") as f:
                    return f.read(), None
        except FileNotFoundError:
            return (
                None,
                "Typst command not found. Please ensure it is in your system's PATH.",
            )
        except Exception as e:
            return None, f"An error occurred during PDF generation: {e}"

    async def export_recipe_as_pdf(self):
        if self.current_recipe_id is None:
            return

        recipe = self.lib.get_recipe(self.current_recipe_id)
        if not recipe:
            return

        default_filename = f"{recipe.title}.pdf"
        filepath, _ = QFileDialog.getSaveFileName(
            self, "Export Recipe as PDF", default_filename, "PDF Files (*.pdf)"
        )
        if not filepath:
            return

        self.statusBar().showMessage(f"Exporting '{recipe.title}' to PDF...", 3000)
        QApplication.setOverrideCursor(Qt.WaitCursor)

        try:
            typst_source = TypstRenderer.render([recipe])
            pdf_data, error = await asyncio.to_thread(
                self._run_typst_in_thread, typst_source
            )

            if error:
                QMessageBox.critical(self, "Export Failed", error)
                self.statusBar().showMessage("Export failed.", 5000)
            else:
                with open(filepath, "wb") as f:
                    f.write(pdf_data)
                self.statusBar().showMessage(
                    f"Successfully exported to {Path(filepath).name}", 5000
                )
        except Exception as e:
            QMessageBox.critical(
                self, "Export Error", f"An unexpected error occurred: {e}"
            )
            self.statusBar().showMessage("Export failed.", 5000)
        finally:
            QApplication.restoreOverrideCursor()

    async def export_cookbook(self):
        all_recipes = self.lib.list_recipes()
        if not all_recipes:
            QMessageBox.information(
                self,
                "Export Cookbook",
                "There are no recipes in the library to export.",
            )
            return

        title, ok = QInputDialog.getText(
            self,
            "Export Cookbook",
            "Enter a title for the cookbook:",
            text="My Recipes",
        )
        if not ok or not title.strip():
            return

        subtitle, ok = QInputDialog.getText(
            self, "Export Cookbook", "Enter an optional subtitle for the cookbook:"
        )
        if not ok:
            return

        filepath, _ = QFileDialog.getSaveFileName(
            self, "Export Cookbook as PDF", f"{title}.pdf", "PDF Files (*.pdf)"
        )
        if not filepath:
            return

        self.statusBar().showMessage("Exporting cookbook to PDF...", 3000)
        QApplication.setOverrideCursor(Qt.WaitCursor)

        try:
            typst_source = TypstRenderer.render(
                all_recipes, title=title, subtitle=subtitle
            )
            pdf_data, error = await asyncio.to_thread(
                self._run_typst_in_thread, typst_source
            )

            if error:
                QMessageBox.critical(self, "Export Failed", error)
                self.statusBar().showMessage("Export failed.", 5000)
            else:
                with open(filepath, "wb") as f:
                    f.write(pdf_data)
                self.statusBar().showMessage(
                    f"Successfully exported cookbook to {Path(filepath).name}", 5000
                )
        except Exception as e:
            QMessageBox.critical(
                self, "Export Error", f"An unexpected error occurred: {e}"
            )
            self.statusBar().showMessage("Export failed.", 5000)
        finally:
            QApplication.restoreOverrideCursor()

    def closeEvent(self, event):
        if self._prompt_save_if_dirty():
            self.lib.close()
            event.accept()
        else:
            event.ignore()


def main():
    app = QApplication(sys.argv)
    app.setStyle("fusion")
    window = MainWindow()
    window.show()
    sys.exit(QtAsyncio.run())


if __name__ == "__main__":
    main()
