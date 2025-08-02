import pytest
from src.recipe_box import Recipe, Component, Step


def test_parse_simple_recipe_with_implicit_component():
    """
    Tests a basic recipe with one implicit component, where ingredients
    are correctly associated with their parent step.
    """
    recipe_text = """
= My Recipe
# First step
- Ingredient A
- Ingredient B
# Second step
"""
    recipe = Recipe.parse(recipe_text)
    assert recipe.title == "My Recipe"
    assert recipe.notes is None
    assert len(recipe.components) == 1

    component = recipe.components[0]
    assert component.name is None
    assert len(component.steps) == 2

    step1 = component.steps[0]
    assert step1.text == "First step"
    assert step1.ingredients == ["Ingredient A", "Ingredient B"]

    step2 = component.steps[1]
    assert step2.text == "Second step"
    assert step2.ingredients is None


def test_parse_recipe_with_named_components():
    """
    Tests a recipe with multiple, explicitly named components.
    """
    recipe_text = """
= Another Recipe
+ Dough
# Mix flour and water.
- Flour
- Water
+ Filling
# Combine ingredients.
- Cheese
- Sauce
"""
    recipe = Recipe.parse(recipe_text)
    assert recipe.title == "Another Recipe"
    assert len(recipe.components) == 2

    comp1 = recipe.components[0]
    assert comp1.name == "Dough"
    assert len(comp1.steps) == 1
    assert comp1.steps[0].text == "Mix flour and water."
    assert comp1.steps[0].ingredients == ["Flour", "Water"]

    comp2 = recipe.components[1]
    assert comp2.name == "Filling"
    assert len(comp2.steps) == 1
    assert comp2.steps[0].text == "Combine ingredients."
    assert comp2.steps[0].ingredients == ["Cheese", "Sauce"]


def test_parse_notes():
    recipe_text = """
= Fancy Recipe
> This is a note.
> It has two lines.
# A single step
"""
    recipe = Recipe.parse(recipe_text)
    assert recipe.title == "Fancy Recipe"
    assert recipe.notes == "This is a note.\nIt has two lines."
    assert len(recipe.components) == 1
    assert len(recipe.components[0].steps) == 1


def test_ingredient_before_step_raises_error():
    """
    Ensures an ingredient cannot appear before a step is defined.
    """
    recipe_text = """
= Another Bad Recipe
- An ingredient with no step
# Step
"""
    with pytest.raises(ValueError, match="Ingredients must belong to a step."):
        Recipe.parse(recipe_text)


def test_empty_recipe_raises_error():
    """
    An empty or whitespace-only string is not a valid recipe and should fail.
    """
    with pytest.raises(ValueError, match="Cannot parse an empty recipe."):
        Recipe.parse("")
    with pytest.raises(ValueError, match="Cannot parse an empty recipe."):
        Recipe.parse("   \n\n  ")


def test_recipe_with_no_title_raises_error():
    """
    A recipe must have a title.
    """
    recipe_text = """
> a note
# a step
"""
    with pytest.raises(ValueError, match="No recipe title."):
        Recipe.parse(recipe_text)
