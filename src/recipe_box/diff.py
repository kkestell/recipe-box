from __future__ import annotations

import difflib

from PySide6.QtGui import QFont
from PySide6.QtWidgets import QWidget, QVBoxLayout, QTextEdit

from src.recipe_box.editor import RecipeHighlighter
from src.recipe_box.preferences import Preferences


class DiffViewer(QWidget):
    def __init__(self, text1: str = "", text2: str = ""):
        super().__init__()
        self.prefs = Preferences.instance()

        self.setWindowTitle("Diff Viewer")
        self.setGeometry(100, 100, 800, 800)

        self.editor = QTextEdit()
        self.highlighter = RecipeHighlighter(self.editor.document())

        self.editor.setReadOnly(True)
        self.editor.setLineWrapMode(QTextEdit.LineWrapMode.WidgetWidth)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)
        layout.addWidget(self.editor)

        self.prefs.preferencesChanged.connect(self.apply_preferences)

        self.apply_preferences()
        self.set_texts(text1, text2)

    def apply_preferences(self):
        font = QFont()
        if self.prefs.data.editor.font_family:
            font.setFamily(self.prefs.data.editor.font_family)
        if self.prefs.data.editor.font_size:
            font.setPointSize(self.prefs.data.editor.font_size)
        self.editor.setFont(font)

        colors = self.prefs.get_theme_colors()
        self.editor.setStyleSheet(
            f"QTextEdit {{ background-color: {colors['background']}; color: {colors['text']};}}"
        )
        self.highlighter.update_colors(colors)

    def setEnabled(self, enabled: bool) -> None:
        self.editor.setEnabled(enabled)

    def set_texts(self, text1: str, text2: str):
        self.text1 = text1
        self.text2 = text2
        self.populate_diff()

    def populate_diff(self):
        self.editor.blockSignals(True)

        if not self.text2 or self.text1 == self.text2:
            self.editor.setText(self.text1)
        else:
            diff_lines = list(
                difflib.unified_diff(
                    self.text1.splitlines(),
                    self.text2.splitlines(),
                    fromfile="Original",
                    tofile="Modified",
                    lineterm="",
                    n=max(len(self.text1.splitlines()), len(self.text2.splitlines())),
                )
            )

            processed_lines = []
            for line in diff_lines:
                if (
                    line.startswith("--- ")
                    or line.startswith("+++ ")
                    or line.startswith("@@")
                ):
                    continue
                elif line.startswith("+"):
                    processed_lines.append(RecipeHighlighter.ADDED_MARKER + line[1:])
                elif line.startswith("-"):
                    processed_lines.append(RecipeHighlighter.DELETED_MARKER + line[1:])
                elif line.startswith(" "):
                    processed_lines.append(line[1:])
                else:
                    processed_lines.append(line)

            self.editor.setText("\n".join(processed_lines))

        self.editor.blockSignals(False)
