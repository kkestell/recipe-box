from __future__ import annotations
import sqlite3
from dataclasses import replace
from pathlib import Path
from src.recipe_box import Recipe


class Library:
    def __init__(self, db_path: str | Path):
        self._db_path = Path(db_path).expanduser()
        db_existed = self._db_path.exists()
        self._db_path.parent.mkdir(parents=True, exist_ok=True)
        self._conn = sqlite3.connect(self._db_path)
        self._conn.row_factory = sqlite3.Row
        self._conn.text_factory = str
        if not db_existed:
            self._create_table()
            self._initial_import()

    def _create_table(self):
        with self._conn:
            self._conn.execute(
                "CREATE TABLE IF NOT EXISTS recipes (id INTEGER PRIMARY KEY, content TEXT NOT NULL)"
            )

    def _initial_import(self, folder_path: str = "~/Documents/Recipes"):
        recipe_dir = Path(folder_path).expanduser()
        if not recipe_dir.is_dir():
            print(f"Initial import directory not found: {recipe_dir}")
            return
        for file_path in recipe_dir.glob("*.txt"):
            try:
                with file_path.open("r", encoding="utf-8") as f:
                    content = f.read()
                if content.strip():
                    recipe = Recipe.parse(content)
                    self.add_recipe(recipe)
                    print(f"Imported: {recipe.title}")
            except Exception as e:
                print(f"Failed to import {file_path.name}: {e}")

    def add_recipe(self, recipe: Recipe) -> int:
        content = recipe.serialize()
        with self._conn:
            cursor = self._conn.execute(
                "INSERT INTO recipes (content) VALUES (?)", (content,)
            )
            return cursor.lastrowid

    def get_recipe(self, recipe_id: int) -> Recipe | None:
        cursor = self._conn.execute(
            "SELECT content FROM recipes WHERE id = ?", (recipe_id,)
        )
        row = cursor.fetchone()
        if row is None:
            return None
        recipe = Recipe.parse(row["content"])
        return replace(recipe, id=recipe_id)

    def update_recipe(self, recipe: Recipe):
        if recipe.id is None:
            raise ValueError("Recipe must have an ID to be updated.")
        content = recipe.serialize()
        with self._conn:
            self._conn.execute(
                "UPDATE recipes SET content = ? WHERE id = ?", (content, recipe.id)
            )

    def delete_recipe(self, recipe_id: int):
        with self._conn:
            self._conn.execute("DELETE FROM recipes WHERE id = ?", (recipe_id,))

    def list_recipes(self) -> list[Recipe]:
        recipes = []
        cursor = self._conn.execute("SELECT id, content FROM recipes")
        for row in cursor.fetchall():
            try:
                recipe = Recipe.parse(row["content"])
                recipes.append(replace(recipe, id=row["id"]))
            except ValueError as e:
                print(f"Warning: Skipping malformed recipe with ID {row['id']}: {e}")
        return recipes

    def close(self):
        self._conn.close()
