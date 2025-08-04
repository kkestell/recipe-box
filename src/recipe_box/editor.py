from __future__ import annotations

from PySide6.QtCore import Signal, QMimeData
from PySide6.QtGui import QSyntaxHighlighter, QFont, QTextCharFormat, QColor
from PySide6.QtWidgets import QTextEdit

from src.recipe_box.preferences import Preferences


class RecipeHighlighter(QSyntaxHighlighter):
    ADDED_MARKER = "\u200b"
    DELETED_MARKER = "\u200c"

    def __init__(self, parent=None):
        super().__init__(parent)
        self._in_metadata_state = 1
        self.colors = None
        self.metadata_format = None
        self.diff_added_format = None
        self.diff_added_prefix_format = None
        self.diff_deleted_format = None
        self.diff_deleted_prefix_format = None
        self.prefix_format = None
        self.title_format = None
        self.notes_format = None
        self.component_format = None
        self.step_format = None
        self.ingredient_format = None

    def update_colors(self, colors):
        self.colors = colors

        self.metadata_format = QTextCharFormat()
        self.metadata_format.setForeground(QColor(self.colors["metadata"]))
        self.metadata_format.setFontFamily("Source Code Pro")

        self.diff_added_format = QTextCharFormat()
        self.diff_added_format.setForeground(
            QColor(self.colors["diff_added_foreground"])
        )
        self.diff_added_format.setBackground(
            QColor(self.colors["diff_added_background"])
        )

        self.diff_added_prefix_format = QTextCharFormat()
        self.diff_added_prefix_format.setForeground(
            QColor(self.colors["diff_added_foreground"])
        )
        self.diff_added_prefix_format.setBackground(
            QColor(self.colors["diff_added_background"])
        )
        self.diff_added_prefix_format.setFontFamily("Source Code Pro")

        self.diff_deleted_format = QTextCharFormat()
        self.diff_deleted_format.setForeground(
            QColor(self.colors["diff_deleted_foreground"])
        )
        self.diff_deleted_format.setBackground(
            QColor(self.colors["diff_deleted_background"])
        )
        self.diff_deleted_format.setFontStrikeOut(True)

        self.diff_deleted_prefix_format = QTextCharFormat()
        self.diff_deleted_prefix_format.setForeground(
            QColor(self.colors["diff_deleted_foreground"])
        )
        self.diff_deleted_prefix_format.setBackground(
            QColor(self.colors["diff_deleted_background"])
        )
        self.diff_deleted_prefix_format.setFontFamily("Source Code Pro")
        self.diff_deleted_prefix_format.setFontStrikeOut(True)

        self.prefix_format = QTextCharFormat()
        self.prefix_format.setForeground(QColor(self.colors["prefix"]))
        self.prefix_format.setFontFamily("Source Code Pro")

        self.title_format = QTextCharFormat()
        self.title_format.setForeground(QColor(self.colors["title"]))
        self.title_format.setFontWeight(QFont.Weight.Bold)

        self.notes_format = QTextCharFormat()
        self.notes_format.setForeground(QColor(self.colors["notes"]))

        self.component_format = QTextCharFormat()
        self.component_format.setForeground(QColor(self.colors["component"]))
        self.component_format.setFontWeight(QFont.Weight.DemiBold)

        self.step_format = QTextCharFormat()
        self.step_format.setForeground(QColor(self.colors["step"]))

        self.ingredient_format = QTextCharFormat()
        self.ingredient_format.setForeground(QColor(self.colors["ingredient"]))

        self.rehighlight()

    def highlightBlock(self, text: str):
        if not self.colors or len(text) == 0:
            return

        is_delimiter = text.strip() == "---"
        is_in_metadata = self.previousBlockState() == self._in_metadata_state
        if is_in_metadata or is_delimiter:
            self.setFormat(0, len(text), self.metadata_format)
            if is_delimiter:
                if not is_in_metadata:
                    self.setCurrentBlockState(self._in_metadata_state)
            else:
                self.setCurrentBlockState(self._in_metadata_state)
            return

        prefix = text[0]
        if prefix in [self.ADDED_MARKER, self.DELETED_MARKER]:
            fmt = None
            prefix_fmt = None
            if text.startswith(self.ADDED_MARKER):
                fmt = self.diff_added_format
                prefix_fmt = self.diff_added_prefix_format
            elif text.startswith(self.DELETED_MARKER):
                fmt = self.diff_deleted_format
                prefix_fmt = self.diff_deleted_prefix_format
            self.setFormat(0, len(text), fmt)
            self.setFormat(1, 1, prefix_fmt)
        elif prefix in ["=", ">", "+", "#", "-"]:
            fmt = None
            if prefix == "=":
                fmt = self.title_format
            elif prefix == ">":
                fmt = self.notes_format
            elif prefix == "+":
                fmt = self.component_format
            elif prefix == "#":
                fmt = self.step_format
            elif prefix == "-":
                fmt = self.ingredient_format
            self.setFormat(0, len(text), fmt)
            self.setFormat(0, 1, self.prefix_format)

        self.setCurrentBlockState(0)


class RecipeEditor(QTextEdit):
    dirtyStateChanged = Signal(bool)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.prefs = Preferences.instance()
        self.highlighter = RecipeHighlighter(self.document())
        self.prefs.preferencesChanged.connect(self.apply_preferences)
        self.document().modificationChanged.connect(self.dirtyStateChanged)
        self.apply_preferences()

    def apply_preferences(self):
        font = QFont()
        if self.prefs.data.editor.font_family:
            font.setFamily(self.prefs.data.editor.font_family)
        if self.prefs.data.editor.font_size:
            font.setPointSize(self.prefs.data.editor.font_size)
        self.setFont(font)
        colors = self.prefs.get_theme_colors()
        self.setStyleSheet(
            f"QTextEdit {{ background-color: {colors['background']}; color: {colors['text']}; }}"
        )
        self.highlighter.update_colors(colors)

    def set_content(self, text: str):
        self.setPlainText(text)

    def is_dirty(self) -> bool:
        return self.document().isModified()

    def insertFromMimeData(self, source: QMimeData):
        if source.hasText():
            self.insertPlainText(source.text())
        else:
            super().insertFromMimeData(source)
