import html
import json
import re
import unicodedata
from fractions import Fraction

import httpx
from bs4 import BeautifulSoup

from src.recipe_box import Recipe


def recipe_from_url(url: str) -> Recipe:
    headers = {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36",
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
        "Accept-Language": "en-US,en;q=0.9",
        "DNT": "1",
        "Upgrade-Insecure-Requests": "1",
    }

    try:
        with httpx.Client(timeout=10.0, follow_redirects=True) as client:
            response = client.get(url, headers=headers)
            response.raise_for_status()
    except httpx.RequestError as e:
        raise ValueError(f"Failed to fetch URL: {e}") from e

    soup = BeautifulSoup(response.text, "html.parser")
    if not (recipe_json := _extract_recipe_json(soup)):
        raise ValueError("Could not find recipe JSON-LD in the page.")

    recipe_text = _jsonld_to_recipe(recipe_json)
    if not (parsed_recipe := Recipe.parse(recipe_text)):
        raise ValueError(f"Failed to parse the generated recipe text:\n\n{recipe_text}")

    return parsed_recipe


def _extract_recipe_json(soup: BeautifulSoup):
    def is_recipe(item):
        if not isinstance(item, dict):
            return False
        item_type = item.get("@type", "")
        if isinstance(item_type, str):
            return "Recipe" in item_type
        if isinstance(item_type, list):
            return "Recipe" in item_type
        return False

    scripts = soup.find_all("script", {"type": "application/ld+json"})
    for script in scripts:
        try:
            content = script.get_text()
            if not content:
                continue

            data = json.loads(content)
            potential_items = data if isinstance(data, list) else [data]

            for item in potential_items:
                if is_recipe(item):
                    return item

                if isinstance(item, dict) and "@graph" in item:
                    for graph_item in item["@graph"]:
                        if is_recipe(graph_item):
                            return graph_item

        except (json.JSONDecodeError, AttributeError, TypeError):
            continue

    return None


def _parse_iso8601_duration(duration_str: str) -> str | None:
    if not duration_str or not duration_str.startswith("PT"):
        return duration_str

    hours_match = re.search(r"(\d+)H", duration_str)
    minutes_match = re.search(r"(\d+)M", duration_str)
    hours = int(hours_match.group(1)) if hours_match else 0
    minutes = int(minutes_match.group(1)) if minutes_match else 0

    parts = []
    if hours > 0:
        parts.append(f"{hours} hour{'s' if hours > 1 else ''}")
    if minutes > 0:
        parts.append(f"{minutes} minute{'s' if minutes > 1 else ''}")

    return " ".join(parts) if parts else None


def _clean_html(text: str) -> str:
    if not text:
        return ""

    soup = BeautifulSoup(text, "html.parser")
    text = html.unescape(soup.get_text())
    text = unicodedata.normalize("NFKC", text)

    def decimal_to_fraction(match):
        try:
            num = float(match.group(0))
        except ValueError:
            return match.group(0)

        f = Fraction(num).limit_denominator(16)
        integer_part = int(f)
        fractional_part = f - integer_part

        parts = []
        if integer_part > 0:
            parts.append(str(integer_part))
        if fractional_part > 0:
            parts.append(str(fractional_part))

        return " ".join(parts) or "0"

    text = re.sub(r"\d*\.\d+", decimal_to_fraction, text)
    text = text.replace("\t", " ")
    text = re.sub(r" +", " ", text)
    return text.strip()


def _jsonld_to_recipe(recipe_json: dict) -> str:
    output_lines = []

    metadata = {}
    if prep_time := recipe_json.get("prepTime"):
        if human_readable := _parse_iso8601_duration(prep_time):
            metadata["prep_time"] = human_readable
    if cook_time := recipe_json.get("cookTime"):
        if human_readable := _parse_iso8601_duration(cook_time):
            metadata["cook_time"] = human_readable
    if recipe_yield := recipe_json.get("recipeYield"):
        yield_text = str(
            recipe_yield[0] if isinstance(recipe_yield, list) else recipe_yield
        )
        metadata["yield"] = _clean_html(yield_text)
    if cuisine := recipe_json.get("recipeCuisine"):
        cuisine_text = cuisine[0] if isinstance(cuisine, list) else cuisine
        metadata["cuisine"] = _clean_html(cuisine_text)
    if category := recipe_json.get("recipeCategory"):
        category_text = category[0] if isinstance(category, list) else category
        metadata["category"] = _clean_html(category_text)

    if metadata:
        output_lines.append("---")
        for key, value in sorted(metadata.items()):
            output_lines.append(f"{key}: {value}")
        output_lines.append("---\n")

    if not (title := recipe_json.get("name")):
        raise ValueError("Recipe JSON-LD has no 'name' field.")
    output_lines.append(f"= {_clean_html(title)}\n")

    if description := recipe_json.get("description"):
        cleaned_description = _clean_html(description)
        for line in cleaned_description.split("\n"):
            if line.strip():
                output_lines.append(f"> {line.strip()}")
        output_lines.append("")

    if ingredients := recipe_json.get("recipeIngredient", []):
        output_lines.append("# Gather all ingredients")
        for ingredient in ingredients:
            output_lines.append(f"- {_clean_html(ingredient)}")
        output_lines.append("")

    instructions = recipe_json.get("recipeInstructions", [])
    for item in instructions:
        item_type = item.get("@type", "HowToStep")
        if item_type == "HowToStep":
            if step_text := _clean_html(item.get("text")):
                output_lines.append(f"# {step_text}\n")
        elif item_type == "HowToSection":
            if section_name := _clean_html(item.get("name")):
                output_lines.append(f"+ {section_name}")
            for step in item.get("itemListElement", []):
                if step_text := _clean_html(step.get("text")):
                    output_lines.append(f"# {step_text}\n")
            output_lines.append("")

    return "\n".join(output_lines).strip()
