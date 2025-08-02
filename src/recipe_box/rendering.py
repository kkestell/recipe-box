import re
from collections import defaultdict
from collections.abc import Iterable

from src.recipe_box import Recipe, Step


class TypstRenderer:
    _FRACTION_SLASH_REGEX = re.compile(r"(?<=\d)/(?=\d)")
    _MULTIPLICATION_SIGN_REGEX = re.compile(r"(?<=\d)x(?=\d)")
    _EN_DASH_REGEX = re.compile(r"(?<=\d)-(?=\d)")

    @staticmethod
    def _fancy(text: str | None) -> str:
        if not text or not text.strip():
            return ""

        narrow_nbsp = "\u202f"
        fraction_slash = "\u2044"
        multiplication_sign = "\u00d7"
        en_dash = "\u2013"

        processed_text = text.replace("°F", f"{narrow_nbsp}°F")
        processed_text = TypstRenderer._FRACTION_SLASH_REGEX.sub(
            fraction_slash, processed_text
        )
        processed_text = TypstRenderer._MULTIPLICATION_SIGN_REGEX.sub(
            multiplication_sign, processed_text
        )
        processed_text = TypstRenderer._EN_DASH_REGEX.sub(en_dash, processed_text)

        return processed_text

    @staticmethod
    def _typst_header() -> str:
        return """
#set list(spacing: 0.65em)
#set text(font: "Libertinus Serif", size: 11pt)
#set page("us-letter", margin: (top: 0.75in, bottom: 1in, left: 0.75in, right: 0.75in))
#set enum(spacing: 1.5em)
""".strip()

    @staticmethod
    def _render_step(step: Step, index: int) -> str:
        has_ingredients = bool(step.ingredients)
        ingredient_list = ", ".join(
            f"[{TypstRenderer._fancy(i)}]" for i in (step.ingredients or [])
        )

        return f"""
#grid(
  columns: (2fr, 1fr),
  gutter: 3em,
  [
    #enum.item({index + 1})[{TypstRenderer._fancy(step.text)}]
  ],
  [
    #if {str(has_ingredients).lower()} {{
      block(
        breakable: false,
        list(
          spacing: 1em,
          {ingredient_list}
        )
      )
    }}
  ]
)
""".strip()

    @staticmethod
    def _render_metadata_grid(metadata: dict[str, str]) -> str:
        def get_value(key: str) -> str:
            return metadata.get(key, "").replace('"', '\\"')

        return f"""
#grid(
  columns: (auto, auto, auto, auto, auto),
  column-gutter: 1.5em,
  row-gutter: 0.75em,
  [#align(center)[#text(weight: "bold")[Yield]]],
  [#align(center)[#text(weight: "bold")[Prep Time]]],
  [#align(center)[#text(weight: "bold")[Cook Time]]],
  [#align(center)[#text(weight: "bold")[Category]]],
  [#align(center)[#text(weight: "bold")[Cuisine]]],
  [#align(center)[{get_value("yield")}]],
  [#align(center)[{get_value("prep_time")}]],
  [#align(center)[{get_value("cook_time")}]],
  [#align(center)[{get_value("category")}]],
  [#align(center)[{get_value("cuisine")}]]
)
""".strip()

    @staticmethod
    def _render_title_with_metadata_grid(
        title: str, metadata: dict[str, str], level: int
    ) -> str:
        metadata_grid = TypstRenderer._render_metadata_grid(metadata)
        return f"""
#grid(
  columns: (1fr, auto),
  gutter: 2em,
  align: horizon,
  [#heading(level: {level})[{title}]],
  [
    #align(right)[
      #block[
        #set text(size: 9pt)
        {metadata_grid}
      ]
    ]
  ]
)
""".strip()

    @staticmethod
    def _render_single_recipe(recipe: Recipe, title_heading_level: int = 1) -> str:
        typst = []
        source = recipe.source

        if source and source.strip():
            footer_content = f"#text(8pt)[{TypstRenderer._fancy(source)}] #h(1fr) #text(8pt, [#counter(page).display() / #counter(page).final().at(0)])"
        else:
            footer_content = "#h(1fr) #text(8pt, [#counter(page).display() / #counter(page).final().at(0)]) #h(1fr)"
        typst.append(f"#set page(footer: context [{footer_content}])")

        title = (
            recipe.title if recipe.title and recipe.title.strip() else "Untitled Recipe"
        )
        metadata_keys = ["yield", "prep_time", "cook_time", "category", "cuisine"]
        has_metadata = any(
            recipe.metadata.get(key) and recipe.metadata.get(key).strip()
            for key in metadata_keys
        )

        if has_metadata:
            typst.append(
                TypstRenderer._render_title_with_metadata_grid(
                    title, recipe.metadata, title_heading_level
                )
            )
        else:
            typst.append(f"#heading(level: {title_heading_level})[{title}]")

        typst.append("#v(1.5em)\n#line(length: 100%, stroke: 0.5pt)\n#v(1.5em)")

        for i, component in enumerate(recipe.components):
            if component.name and component.name.strip():
                typst.append(f"=== {component.name}\n#v(1em)")
            for j, step in enumerate(component.steps):
                typst.append(TypstRenderer._render_step(step, j))
                if j < len(component.steps) - 1:
                    typst.append("#v(1em)")
            if i < len(recipe.components) - 1:
                typst.append("#v(3em)")

        return "\n\n".join(typst)

    @staticmethod
    def _render_cookbook(
        recipes: Iterable[Recipe], title: str | None, subtitle: str | None
    ) -> list[str]:
        typst = []

        if (title and title.strip()) or (subtitle and subtitle.strip()):
            typst.append("#v(5cm)")
            typst.append(
                f"#align(center)[#text(size: 22pt)[#heading(level: 1, outlined: false)[{title}]]]"
            )
            if subtitle and subtitle.strip():
                typst.append("#v(1cm)")
                typst.append(
                    f"#align(center)[#heading(level: 2, outlined: false)[{subtitle}]]"
                )
            typst.append("#pagebreak()")

        typst.extend(
            [
                "#align(center)[#heading(level: 1, outlined: false)[Contents]]",
                "#v(1cm)",
                "#outline(title: none, depth: 2)",
                "#pagebreak()",
                "#counter(page).update(1)",
            ]
        )

        recipes_by_category = defaultdict(list)
        for recipe in recipes:
            recipes_by_category[recipe.category].append(recipe)

        sorted_categories = sorted(recipes_by_category.items())

        for cat_idx, (category, cat_recipes) in enumerate(sorted_categories):
            typst.extend(
                [
                    "#v(5cm)",
                    f"#align(center)[#heading(level: 1)[{category}]]",
                    "#pagebreak()",
                ]
            )
            for rec_idx, recipe in enumerate(cat_recipes):
                typst.append(
                    TypstRenderer._render_single_recipe(recipe, title_heading_level=2)
                )
                is_last_recipe = (
                    rec_idx == len(cat_recipes) - 1
                    and cat_idx == len(sorted_categories) - 1
                )
                if not is_last_recipe:
                    typst.append("#pagebreak()")

        return typst

    @classmethod
    def render(
        cls,
        recipes: list[Recipe],
        title: str | None = None,
        subtitle: str | None = None,
    ) -> str:
        typst_parts = [cls._typst_header()]

        if len(recipes) == 1:
            typst_parts.append(cls._render_single_recipe(recipes[0]))
        else:
            typst_parts.extend(cls._render_cookbook(recipes, title, subtitle))

        return "\n\n".join(typst_parts)
