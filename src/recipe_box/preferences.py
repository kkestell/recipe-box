from __future__ import annotations

import json
import os
from copy import deepcopy
from dataclasses import asdict, dataclass, field
from pathlib import Path

from PySide6.QtCore import (
    QAbstractTableModel,
    QEvent,
    QModelIndex,
    QObject,
    Qt,
    Signal,
)
from PySide6.QtWidgets import (
    QAbstractItemView,
    QApplication,
    QComboBox,
    QDialog,
    QDialogButtonBox,
    QFontComboBox,
    QFrame,
    QGridLayout,
    QHBoxLayout,
    QHeaderView,
    QLabel,
    QPushButton,
    QSpinBox,
    QTableView,
    QVBoxLayout,
)

from src.recipe_box.theme import DEFAULT_THEME, MARGIN


@dataclass
class AIProvider:
    model: str | None = "new-provider"
    api_key: str | None = ""


@dataclass
class EditorSettings:
    font_family: str | None = None
    font_size: int | None = None


@dataclass
class UISettings:
    font_family: str | None = None
    font_size: int | None = None


@dataclass
class AppPreferences:
    ai_providers: list[AIProvider] = field(default_factory=list)
    editor: EditorSettings = field(default_factory=EditorSettings)
    ui: UISettings = field(default_factory=UISettings)
    theme: str | None = None


def get_config_dir() -> Path:
    if os.name == "nt":
        appdata = os.getenv("APPDATA")
        if appdata:
            return Path(appdata) / "RecipeBox"
    return Path.home() / ".config" / "RecipeBox"


def get_prefs_path() -> Path:
    return get_config_dir() / "preferences.json"


def get_themes_path() -> Path:
    return get_config_dir() / "themes.json"


class Preferences(QObject):
    _instance = None
    preferencesChanged = Signal()

    @staticmethod
    def instance() -> Preferences:
        if Preferences._instance is None:
            Preferences._instance = Preferences()
        return Preferences._instance

    def __init__(self):
        if Preferences._instance is not None:
            raise RuntimeError(
                "Preferences is a singleton, use Preferences.instance() instead."
            )
        super().__init__()
        self._prefs_path = get_prefs_path()
        self._themes_path = get_themes_path()

        self.data = AppPreferences()
        self.themes: list[Theme] = []

        self._load_themes()
        self.load()

        app = QApplication.instance()
        if app:
            app.installEventFilter(self)

    def eventFilter(self, watched: QObject, event: QEvent) -> bool:
        if event.type() == QEvent.Type.ThemeChange:
            if self.data.theme is None:
                self.preferencesChanged.emit()
        return super().eventFilter(watched, event)

    def _load_themes(self):
        try:
            if not self._themes_path.exists():
                self._create_default_themes_file()

            with open(self._themes_path, "r", encoding="utf-8") as f:
                raw_data = json.load(f)
                raw_themes = raw_data.get("themes", [])
                self.themes = [Theme(**t) for t in raw_themes]
        except (IOError, json.JSONDecodeError) as e:
            print(f"Error loading themes: {e}")
            default_themes = deepcopy(DEFAULT_THEME["themes"])
            self.themes = [Theme(**t) for t in default_themes]

    def _create_default_themes_file(self):
        try:
            self._themes_path.parent.mkdir(parents=True, exist_ok=True)
            with open(self._themes_path, "w", encoding="utf-8") as f:
                json.dump(DEFAULT_THEME, f, indent=4)
        except IOError as e:
            print(f"Error creating default themes file: {e}")

    def load(self):
        try:
            with open(self._prefs_path, "r", encoding="utf-8") as f:
                raw_data = json.load(f)

                raw_providers = raw_data.get("ai_providers", [])
                providers = [AIProvider(**p) for p in raw_providers]

                editor_data = raw_data.get("editor", {})
                editor_settings = EditorSettings(**editor_data)

                ui_data = raw_data.get("ui", {})
                ui_settings = UISettings(**ui_data)

                self.data = AppPreferences(
                    ai_providers=providers,
                    editor=editor_settings,
                    ui=ui_settings,
                    theme=raw_data.get("theme"),
                )
        except (FileNotFoundError, json.JSONDecodeError):
            self.data = AppPreferences()

    def save(self):
        try:
            self._prefs_path.parent.mkdir(parents=True, exist_ok=True)
            data_to_save = asdict(self.data)
            with open(self._prefs_path, "w", encoding="utf-8") as f:
                json.dump(data_to_save, f, indent=4)
            self.preferencesChanged.emit()
        except IOError as e:
            print(f"Error saving preferences: {e}")

    def get_theme_colors(self) -> dict[str, str]:
        theme_name_to_use = self.data.theme

        if not theme_name_to_use:
            app = QApplication.instance()
            if app and app.styleHints().colorScheme() == Qt.ColorScheme.Dark:
                theme_name_to_use = "Dark"
            else:
                theme_name_to_use = "Light"

        for theme_data in self.themes:
            if theme_data.name == theme_name_to_use:
                return theme_data.colors

        for theme_data in self.themes:
            if theme_data.name == "Light":
                return theme_data.colors

        return deepcopy(DEFAULT_THEME["themes"][0]["colors"])


@dataclass
class Theme:
    name: str
    colors: dict[str, str]


class ProviderModel(QAbstractTableModel):
    def __init__(self, providers: list[AIProvider], parent=None):
        super().__init__(parent)
        self._providers = providers
        self._headers = ["Model", "API Key"]

    def rowCount(self, parent=QModelIndex()):
        return len(self._providers)

    def columnCount(self, parent=QModelIndex()):
        return len(self._headers)

    def data(self, index: QModelIndex, role=Qt.ItemDataRole.DisplayRole):
        if not index.isValid():
            return None

        provider = self._providers[index.row()]
        col = index.column()

        if role == Qt.ItemDataRole.DisplayRole:
            if col == 0:
                return provider.model
            if col == 1:
                return "********" if provider.api_key else ""
        elif role == Qt.ItemDataRole.EditRole:
            if col == 0:
                return provider.model
            if col == 1:
                return provider.api_key
        return None

    def setData(self, index: QModelIndex, value: str, role=Qt.ItemDataRole.EditRole):
        if not index.isValid() or role != Qt.ItemDataRole.EditRole:
            return False

        provider = self._providers[index.row()]
        col = index.column()

        if col == 0:
            provider.model = value or None
        elif col == 1:
            provider.api_key = value or None
        else:
            return False

        self.dataChanged.emit(index, index, [role])
        return True

    def flags(self, index: QModelIndex):
        return super().flags(index) | Qt.ItemFlag.ItemIsEditable

    def headerData(self, section, orientation, role=Qt.ItemDataRole.DisplayRole):
        if (
            role == Qt.ItemDataRole.DisplayRole
            and orientation == Qt.Orientation.Horizontal
        ):
            return self._headers[section]
        return None


class PreferencesDialog(QDialog):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("Preferences")
        self.setMinimumSize(500, 400)
        self.resize(500, 400)

        self.prefs = Preferences.instance()
        self.providers = deepcopy(self.prefs.data.ai_providers)
        self.model = ProviderModel(self.providers, self)

        main_layout = QVBoxLayout(self)
        general_group = QFrame()
        general_group.setFrameShape(QFrame.Shape.StyledPanel)
        ui_font_group = QFrame()
        ui_font_group.setFrameShape(QFrame.Shape.StyledPanel)
        provider_group = QFrame()
        provider_group.setFrameShape(QFrame.Shape.StyledPanel)

        self._setup_general_ui(general_group)
        self._setup_ui_font_ui(ui_font_group)
        self._setup_provider_ui(provider_group)

        button_box = QDialogButtonBox(
            QDialogButtonBox.StandardButton.Ok
            | QDialogButtonBox.StandardButton.Cancel
            | QDialogButtonBox.StandardButton.Apply
        )

        main_layout.addWidget(general_group)
        main_layout.addWidget(ui_font_group)
        main_layout.addWidget(provider_group)
        main_layout.addWidget(button_box)

        button_box.accepted.connect(self.accept)
        button_box.rejected.connect(self.reject)
        button_box.button(QDialogButtonBox.StandardButton.Apply).clicked.connect(
            self.apply_changes
        )
        self.add_button.clicked.connect(self.add_provider)
        self.remove_button.clicked.connect(self.remove_provider)

    def _setup_general_ui(self, parent_widget):
        layout = QGridLayout(parent_widget)
        layout.setSpacing(MARGIN)
        layout.setContentsMargins(MARGIN, MARGIN, MARGIN, MARGIN)

        # Theme
        self.theme_combo = QComboBox()
        self.theme_combo.insertItem(0, "System")
        theme_names = [theme.name for theme in self.prefs.themes]
        self.theme_combo.addItems(theme_names)
        if self.prefs.data.theme:
            self.theme_combo.setCurrentText(self.prefs.data.theme)
        else:
            self.theme_combo.setCurrentIndex(0)

        # Editor Font
        self.editor_font_family_combo = QFontComboBox()
        self.editor_font_family_combo.insertItem(0, "System Default")
        if self.prefs.data.editor.font_family:
            self.editor_font_family_combo.setCurrentText(
                self.prefs.data.editor.font_family
            )
        else:
            self.editor_font_family_combo.setCurrentIndex(0)

        self.editor_font_size_spinbox = QSpinBox()
        self.editor_font_size_spinbox.setFixedWidth(75)
        self.editor_font_size_spinbox.setRange(8, 72)
        self.editor_font_size_spinbox.setSpecialValueText("Default")
        if self.prefs.data.editor.font_size:
            self.editor_font_size_spinbox.setValue(self.prefs.data.editor.font_size)
        else:
            self.editor_font_size_spinbox.setValue(
                self.editor_font_size_spinbox.minimum() - 1
            )

        editor_font_layout = QHBoxLayout()
        editor_font_layout.setSpacing(MARGIN)
        editor_font_layout.setContentsMargins(0, 0, 0, 0)
        editor_font_layout.addWidget(self.editor_font_family_combo, 1)
        editor_font_layout.addWidget(self.editor_font_size_spinbox)

        # Layout
        layout.addWidget(QLabel("Theme:"), 0, 0)
        layout.addWidget(self.theme_combo, 0, 1)
        layout.addWidget(QLabel("Editor Font:"), 1, 0)
        layout.addLayout(editor_font_layout, 1, 1)

    def _setup_ui_font_ui(self, parent_widget):
        layout = QGridLayout(parent_widget)
        layout.setSpacing(MARGIN)
        layout.setContentsMargins(MARGIN, MARGIN, MARGIN, MARGIN)

        self.ui_font_family_combo = QFontComboBox()
        self.ui_font_family_combo.insertItem(0, "System Default")
        if self.prefs.data.ui.font_family:
            self.ui_font_family_combo.setCurrentText(self.prefs.data.ui.font_family)
        else:
            self.ui_font_family_combo.setCurrentIndex(0)

        self.ui_font_size_spinbox = QSpinBox()
        self.ui_font_size_spinbox.setFixedWidth(75)
        self.ui_font_size_spinbox.setRange(8, 72)
        self.ui_font_size_spinbox.setSpecialValueText("Default")
        if self.prefs.data.ui.font_size:
            self.ui_font_size_spinbox.setValue(self.prefs.data.ui.font_size)
        else:
            self.ui_font_size_spinbox.setValue(self.ui_font_size_spinbox.minimum() - 1)

        ui_font_layout = QHBoxLayout()
        ui_font_layout.setSpacing(MARGIN)
        ui_font_layout.setContentsMargins(0, 0, 0, 0)
        ui_font_layout.addWidget(self.ui_font_family_combo, 1)
        ui_font_layout.addWidget(self.ui_font_size_spinbox)

        layout.addWidget(QLabel("UI Font:"), 0, 0)
        layout.addLayout(ui_font_layout, 0, 1)

    def _setup_provider_ui(self, parent_widget):
        layout = QVBoxLayout(parent_widget)
        layout.setSpacing(MARGIN)
        layout.setContentsMargins(MARGIN, MARGIN, MARGIN, MARGIN)

        self.provider_table = QTableView()
        self.provider_table.setModel(self.model)
        self.provider_table.setSelectionBehavior(
            QAbstractItemView.SelectionBehavior.SelectRows
        )
        self.provider_table.verticalHeader().setVisible(False)
        self.provider_table.setAlternatingRowColors(True)

        header = self.provider_table.horizontalHeader()
        header.setSectionResizeMode(0, QHeaderView.ResizeMode.Stretch)
        header.setSectionResizeMode(1, QHeaderView.ResizeMode.Stretch)

        self.add_button = QPushButton("Add")
        self.remove_button = QPushButton("Remove")
        button_layout = QHBoxLayout()
        button_layout.addStretch()
        button_layout.addWidget(self.add_button)
        button_layout.addWidget(self.remove_button)

        layout.addWidget(QLabel("AI Providers:"))
        layout.addWidget(self.provider_table)
        layout.addLayout(button_layout)

    def _save_preferences(self):
        if self.theme_combo.currentIndex() == 0:
            self.prefs.data.theme = None
        else:
            self.prefs.data.theme = self.theme_combo.currentText()

        # Editor font
        if self.editor_font_family_combo.currentIndex() == 0:
            self.prefs.data.editor.font_family = None
        else:
            self.prefs.data.editor.font_family = (
                self.editor_font_family_combo.currentText()
            )

        font_size = self.editor_font_size_spinbox.value()
        if font_size < self.editor_font_size_spinbox.minimum():
            self.prefs.data.editor.font_size = None
        else:
            self.prefs.data.editor.font_size = font_size

        # UI font
        if self.ui_font_family_combo.currentIndex() == 0:
            self.prefs.data.ui.font_family = None
        else:
            self.prefs.data.ui.font_family = self.ui_font_family_combo.currentText()

        ui_font_size = self.ui_font_size_spinbox.value()
        if ui_font_size < self.ui_font_size_spinbox.minimum():
            self.prefs.data.ui.font_size = None
        else:
            self.prefs.data.ui.font_size = ui_font_size

        self.prefs.data.ai_providers = self.providers
        self.prefs.save()

    def apply_changes(self):
        self._save_preferences()

    def add_provider(self):
        row_count = self.model.rowCount()
        self.model.beginInsertRows(QModelIndex(), row_count, row_count)
        self.providers.append(AIProvider())
        self.model.endInsertRows()
        self.provider_table.selectRow(row_count)
        self.provider_table.edit(self.model.index(row_count, 0))

    def remove_provider(self):
        selection_model = self.provider_table.selectionModel()
        if not selection_model.hasSelection():
            return

        selected_rows = sorted(
            {index.row() for index in selection_model.selectedIndexes()}, reverse=True
        )

        for row in selected_rows:
            self.model.beginRemoveRows(QModelIndex(), row, row)
            del self.providers[row]
            self.model.endRemoveRows()

    def accept(self):
        self._save_preferences()
        super().accept()
