from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QStandardItem, QStandardItemModel, QBrush
from PySide6.QtWidgets import (
    QLineEdit,
    QTreeView,
    QVBoxLayout,
    QWidget,
)

from src.recipe_box import Recipe
from src.recipe_box.theme import MARGIN


class RecipeTreeView(QTreeView):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setHeaderHidden(True)


class RecipeTreeModel(QStandardItemModel):
    def __init__(self, parent=None):
        super().__init__(parent)

    def populate(self, recipes: list[Recipe]):
        self.clear()
        root_node = self.invisibleRootItem()
        categories = {}
        for r in recipes:
            categories.setdefault(r.category, []).append(r)
        for category_name in sorted(categories.keys()):
            category_item = QStandardItem(category_name)
            category_item.setEditable(False)
            category_item.setSelectable(False)
            root_node.appendRow(category_item)
            for recipe in sorted(
                categories[category_name], key=lambda r: (r.draft is not None, r.title)
            ):
                recipe_item = QStandardItem(recipe.title)
                recipe_item.setData(recipe.id, Qt.ItemDataRole.UserRole)
                recipe_item.setEditable(False)

                if recipe.draft is not None:
                    recipe_item.setForeground(QBrush(Qt.GlobalColor.gray))

                if recipe.favorite:
                    font = recipe_item.font()
                    font.setBold(True)
                    recipe_item.setFont(font)

                category_item.appendRow(recipe_item)

    def find_item_by_id(self, recipe_id: int) -> QStandardItem | None:
        root = self.invisibleRootItem()
        for i in range(root.rowCount()):
            category_item = root.child(i)
            for j in range(category_item.rowCount()):
                recipe_item = category_item.child(j)
                if recipe_item.data(Qt.ItemDataRole.UserRole) == recipe_id:
                    return recipe_item
        return None


class RecipeBrowser(QWidget):
    recipeSelected = Signal(object)

    def __init__(self, parent=None):
        super().__init__(parent)
        self._layout = QVBoxLayout(self)
        self._layout.setContentsMargins(MARGIN, MARGIN, 0, 0)
        self._layout.setSpacing(MARGIN)
        self._filter_edit = QLineEdit()
        self._filter_edit.setPlaceholderText("Filter recipes...")
        self._tree_view = RecipeTreeView()
        self._tree_model = RecipeTreeModel()
        self._tree_view.setModel(self._tree_model)
        self._layout.addWidget(self._filter_edit)
        self._layout.addWidget(self._tree_view)
        self._filter_edit.textChanged.connect(self._filter_recipes)
        self._tree_view.selectionModel().currentChanged.connect(
            self._on_selection_changed
        )

    def populate(self, recipes: list[Recipe]):
        selected_id = self.selected_recipe_id()
        v_scroll_val = self._tree_view.verticalScrollBar().value()
        h_scroll_val = self._tree_view.horizontalScrollBar().value()
        if self._tree_model.rowCount() > 0:
            collapsed_categories = {
                self._tree_model.item(i).text()
                for i in range(self._tree_model.rowCount())
                if not self._tree_view.isExpanded(self._tree_model.index(i, 0))
            }
        else:
            collapsed_categories = set()
        self._tree_view.selectionModel().blockSignals(True)
        self._tree_model.populate(recipes)
        self._tree_view.expandAll()
        root = self._tree_model.invisibleRootItem()
        for i in range(root.rowCount()):
            category_item = root.child(i)
            if category_item.text() in collapsed_categories:
                self._tree_view.collapse(category_item.index())
        self.select_recipe(selected_id)
        self._filter_recipes(self._filter_edit.text())
        self._tree_view.verticalScrollBar().setValue(v_scroll_val)
        self._tree_view.horizontalScrollBar().setValue(h_scroll_val)
        self._tree_view.selectionModel().blockSignals(False)

    def selected_recipe_id(self) -> int | None:
        current_index = self._tree_view.currentIndex()
        if not current_index.isValid():
            return None
        item = self._tree_model.itemFromIndex(current_index)
        if item:
            return item.data(Qt.ItemDataRole.UserRole)
        return None

    def select_recipe(self, recipe_id: int | None):
        selection_model = self._tree_view.selectionModel()
        if recipe_id is None:
            selection_model.clear()
            return
        item = self._tree_model.find_item_by_id(recipe_id)
        if item:
            index = item.index()
            self._tree_view.setCurrentIndex(index)
        else:
            selection_model.clear()

    def _on_selection_changed(self, current_index, previous_index):
        recipe_id = self.selected_recipe_id()
        self.recipeSelected.emit(recipe_id)

    def _filter_recipes(self, text: str):
        filter_text = text.lower()
        root = self._tree_model.invisibleRootItem()
        for i in range(root.rowCount()):
            category_item = root.child(i)
            category_match = False
            for j in range(category_item.rowCount()):
                recipe_item = category_item.child(j)
                recipe_visible = filter_text in recipe_item.text().lower()
                self._tree_view.setRowHidden(
                    j, category_item.index(), not recipe_visible
                )
                if recipe_visible:
                    category_match = True
            self._tree_view.setRowHidden(i, root.index(), not category_match)
