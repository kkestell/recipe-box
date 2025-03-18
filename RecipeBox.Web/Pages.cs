namespace RecipeBox.Web;

public static class Pages
{
    public const string Index = """
                         <!DOCTYPE html>
                         <html lang="en">
                         <head>
                            <meta charset="UTF-8"/>
                            <title>Recipe Box</title>
                            <link rel="stylesheet" href="/static/css/codemirror.min.css">
                            <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/codemirror/6.65.7/addon/fold/foldgutter.min.css">
                            <script src="/static/js/codemirror.min.js"></script>
                            <script src="https://cdnjs.cloudflare.com/ajax/libs/codemirror/6.65.7/addon/fold/foldcode.min.js"></script>
                            <script src="https://cdnjs.cloudflare.com/ajax/libs/codemirror/6.65.7/addon/fold/foldgutter.min.js"></script>
                            <script defer src="https://cdn.jsdelivr.net/npm/alpinejs@3.14.8/dist/cdn.min.js"></script>
                            <script src="/static/js/recipe-mode.js"></script>
                            <link rel="stylesheet" href="/static/css/app.css">
                            <script src="/static/js/app.js"></script>
                         </head>
                         <body x-data>
                            <div class="sidebar">
                                <header :class="$store.recipeStore.searchMode ? 'search-mode' : ''">
                                    <div class="header-default">
                                        <h1>Recipe Box</h1>
                                        <div class="header-buttons">
                                            <button class="search-button" title="Search recipes" @click="$store.recipeStore.toggleSearchMode(true)">
                                                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                                    <circle cx="11" cy="11" r="8"></circle>
                                                    <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
                                                </svg>
                                            </button>
                                            <button class="add-button" title="Add new recipe" @click="$store.recipeStore.createNewRecipe()">
                                                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                                    <line x1="12" y1="5" x2="12" y2="19"></line>
                                                    <line x1="5" y1="12" x2="19" y2="12"></line>
                                                </svg>
                                            </button>
                                        </div>
                                    </div>
                                    <div class="header-search">
                                        <input type="text" class="search-input" placeholder="Search recipes..."
                                               x-model="$store.recipeStore.searchQuery"
                                               @input="$store.recipeStore.filterRecipes($event.target.value)"
                                               @keydown.escape="$store.recipeStore.toggleSearchMode(false)">
                                        <button class="close-search-button" title="Close search" @click="$store.recipeStore.toggleSearchMode(false)">
                                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                                <line x1="18" y1="6" x2="6" y2="18"></line>
                                                <line x1="6" y1="6" x2="18" y2="18"></line>
                                            </svg>
                                        </button>
                                    </div>
                                </header>
                                <ul id="recipe-list" x-data>
                                    <!-- Uncategorized recipes -->
                                    <template x-for="recipe in $store.recipeStore.getUncategorizedRecipes()" :key="recipe.slug">
                                        <li class="recipe-item uncategorized-recipe" x-show="$store.recipeStore.isRecipeVisible(recipe)">
                                            <a href="#" class="recipe" :data-slug="recipe.slug"
                                               :class="{'active': $store.recipeStore.currentSlug === recipe.slug}"
                                               @click.prevent="$store.recipeStore.loadRecipeContent(recipe.slug)">
                                                <span x-text="recipe.title"></span>
                                            </a>
                                        </li>
                                    </template>
                         
                                    <!-- Categorized recipes -->
                                    <template x-for="category in $store.recipeStore.getCategories().filter(cat => cat !== 'Uncategorized')" :key="category">
                                        <li class="category" x-show="$store.recipeStore.isCategoryVisible(category)">
                                            <div class="category-header" @click="$event.target.closest('.category').classList.toggle('collapsed')" x-text="category"></div>
                                            <ul class="category-items">
                                                <!-- Direct category recipes (no subcategory) -->
                                                <template x-for="recipe in $store.recipeStore.getCategoryRecipes(category, null)" :key="recipe.slug">
                                                    <li class="recipe-item" x-show="$store.recipeStore.isRecipeVisible(recipe)">
                                                        <a href="#" class="recipe" :data-slug="recipe.slug"
                                                           :class="{'active': $store.recipeStore.currentSlug === recipe.slug}"
                                                           @click.prevent="$store.recipeStore.loadRecipeContent(recipe.slug)">
                                                            <span x-text="recipe.title"></span>
                                                        </a>
                                                    </li>
                                                </template>
                         
                                                <!-- Subcategories -->
                                                <template x-for="subcategory in $store.recipeStore.getCategorySubcategories(category)" :key="subcategory">
                                                    <li class="subcategory" x-show="$store.recipeStore.isSubcategoryVisible(category, subcategory)">
                                                        <div class="subcategory-header" @click="$event.target.closest('.subcategory').classList.toggle('collapsed')" x-text="subcategory"></div>
                                                        <ul class="subcategory-items">
                                                            <template x-for="recipe in $store.recipeStore.getSubcategoryRecipes(category, subcategory)" :key="recipe.slug">
                                                                <li class="recipe-item" x-show="$store.recipeStore.isRecipeVisible(recipe)">
                                                                    <a href="#" class="recipe" :data-slug="recipe.slug"
                                                                       :class="{'active': $store.recipeStore.currentSlug === recipe.slug}"
                                                                       @click.prevent="$store.recipeStore.loadRecipeContent(recipe.slug)">
                                                                        <span x-text="recipe.title"></span>
                                                                    </a>
                                                                </li>
                                                            </template>
                                                        </ul>
                                                    </li>
                                                </template>
                                            </ul>
                                        </li>
                                    </template>
                                </ul>
                            </div>
                            <div class="content">
                                <div class="toast"
                                     :class="[$store.recipeStore.toast.status, {'hidden': !$store.recipeStore.toast.visible}]"
                                     x-text="$store.recipeStore.toast.message"></div>
                                <div class="editor-container">
                                    <textarea id="editor"></textarea>
                                </div>
                            </div>
                            <div class="context-menu" id="recipeContextMenu">
                                <div class="context-menu-item" data-action="delete">Delete Recipe</div>
                            </div>
                         </body>
                         </html>
                         """;

}