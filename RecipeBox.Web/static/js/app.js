// Global editor instance
let editor;
let editorContainer;

document.addEventListener('DOMContentLoaded', function() {
    // Get the editor container to control visibility
    editorContainer = document.querySelector('.editor-container') || document.getElementById('editor').parentElement;

    // Initially hide the editor container
    if (editorContainer) {
        editorContainer.style.display = 'none';
    }

    // Initialize CodeMirror editor
    editor = CodeMirror.fromTextArea(document.getElementById('editor'), {
        mode: 'recipe',
        lineNumbers: false,
        lineWrapping: true,
        foldGutter: true,
        foldOptions: {
            widget: "---"
        },
        gutters: ['CodeMirror-foldgutter'],
        indentUnit: 4
    });

    // Add change event to track dirty state
    editor.on('change', function() {
        const store = window.Alpine.store('recipeStore');
        const currentContent = editor.getValue();
        const isDirty = currentContent !== store.originalContent;
        store.setDirtyState(isDirty);
        store.debouncedValidate();
    });

    // Global key events
    document.addEventListener('keydown', function(e) {
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
            e.preventDefault();
            window.Alpine.store('recipeStore').handleSave();
            return false;
        }
        if ((e.ctrlKey || e.metaKey) && e.key === 'p') {
            e.preventDefault();
            window.Alpine.store('recipeStore').handlePrintPDF();
            return false;
        }
        if ((e.ctrlKey || e.metaKey) && e.key === 'i') {
            e.preventDefault();
            window.Alpine.store('recipeStore').handleImport();
            return false;
        }
    });

    window.addEventListener('beforeunload', function(e) {
        const store = window.Alpine.store('recipeStore');

        // Make a POST request to /shutdown when the window is closing
        // Using sendBeacon to ensure the request is sent even during page unload
        navigator.sendBeacon('/shutdown');

        if (store.dirty) {
            // Standard way to show a confirmation dialog when closing the browser
            e.preventDefault();
            // The message text is typically ignored by modern browsers,
            // but we set it for older browsers
            e.returnValue = 'You have unsaved changes. Are you sure you want to leave?';
            return e.returnValue;
        }
    });
});

// Alpine.js store for global state and methods
document.addEventListener('alpine:init', () => {
    Alpine.store('recipeStore', {
        currentSlug: null,
        originalContent: "",
        recipes: {},
        toast: {
            message: '',
            status: '',
            visible: false
        },
        searchMode: false,
        searchQuery: '',
        editorVisible: false,
        placeholderRecipe: null, // Track the placeholder recipe
        dirty: false, // Track if the document has unsaved changes

        init() {
            this.debouncedValidate = this.debounce(this.validateContent, 100);

            // Set up event delegation for context menu
            document.addEventListener('contextmenu', (e) => {
                // Check if the click was on a recipe item
                const recipeElement = e.target.closest('.recipe');
                if (!recipeElement) return; // Not clicking on a recipe item

                e.preventDefault();
                const slug = recipeElement.dataset.slug;

                const contextMenu = document.getElementById('recipeContextMenu');
                contextMenu.style.left = e.pageX + 'px';
                contextMenu.style.top = e.pageY + 'px';
                contextMenu.style.display = 'block';
                contextMenu.dataset.slug = slug;
            });

            // Handle context menu actions with a single listener
            document.getElementById('recipeContextMenu').addEventListener('click', (e) => {
                const action = e.target.dataset.action;
                const slug = e.currentTarget.dataset.slug;

                if (action === 'delete' && slug) {
                    if (confirm('Are you sure you want to delete this recipe?')) {
                        this.deleteRecipe(slug);
                    }
                }

                e.currentTarget.style.display = 'none';
            });

            // Close context menu when clicking elsewhere
            document.addEventListener('click', () => {
                document.getElementById('recipeContextMenu').style.display = 'none';
            });

            this.loadRecipes();
        },

        // Set the dirty state and update the window title accordingly
        setDirtyState(isDirty) {
            this.dirty = isDirty;
            this.updateWindowTitle();
        },

        // Update the window title based on the current recipe and dirty state
        updateWindowTitle() {
            // If no recipe is selected or being edited
            if (!this.currentSlug && !this.placeholderRecipe) {
                document.title = 'Recipe Box';
                return;
            }

            const titleMatch = editor.getValue().match(/^=\s*(.+)$/m);
            const baseTitle = titleMatch ? `${titleMatch[1]}` : 'Untitled Recipe';
            document.title = `${this.dirty ? '*' : ''}${baseTitle} – Recipe Box`;
        },

        async loadRecipes() {
            try {
                const response = await fetch('/recipes');
                if (!response.ok) throw new Error('Failed to load recipes');
                this.recipes = await response.json();
            } catch (error) {
                console.error('Error loading recipes:', error);
                this.showToast('error', 'Failed to load recipes');
            }
        },

        setupRecipeClickHandlers() {
            document.querySelectorAll('.recipe').forEach(link => {
                // Context menu handling
                link.addEventListener('contextmenu', (e) => {
                    e.preventDefault();
                    const recipeElement = e.target.closest('.recipe');
                    const slug = recipeElement ? recipeElement.dataset.slug : null;

                    const contextMenu = document.getElementById('recipeContextMenu');
                    contextMenu.style.left = e.pageX + 'px';
                    contextMenu.style.top = e.pageY + 'px';
                    contextMenu.style.display = 'block';
                    contextMenu.dataset.slug = slug;
                });
            });

            document.getElementById('recipeContextMenu').addEventListener('click', (e) => {
                const action = e.target.dataset.action;
                const slug = e.currentTarget.dataset.slug;

                if (action === 'delete' && slug && slug !== 'undefined') {
                    if (confirm('Are you sure you want to delete this recipe?')) {
                        this.deleteRecipe(slug);
                    }
                }

                e.currentTarget.style.display = 'none';
            });
        },

        toggleSearchMode(enabled) {
            this.searchMode = enabled;
            this.searchQuery = '';

            if (enabled) {
                Alpine.nextTick(() => {
                    document.querySelector('.search-input').focus();
                });
            } else {
                this.filterRecipes('');
            }
        },

        filterRecipes(query) {
            this.searchQuery = query;

            // UI updates will be handled by Alpine template
            // The visibility logic is moved to the HTML with x-show directives
        },

        async loadRecipeContent(slug) {
            // Check if current document is dirty before loading a new one
            if (this.dirty) {
                if (!confirm('You have unsaved changes. Do you want to discard them and load another recipe?')) {
                    return; // User cancelled, stay on current recipe
                }
            }

            // If we're switching away from a placeholder recipe, remove it
            // regardless of whether it's dirty or not
            if (!this.currentSlug && this.placeholderRecipe) {
                this.placeholderRecipe = null;
            }

            try {
                const response = await fetch(`/recipes/${encodeURIComponent(slug)}`);
                if (!response.ok) throw new Error('Failed to load recipe content');
                const text = await response.text();
                this.currentSlug = slug;
                this.originalContent = text;
                editor.setValue(text);

                // Reset dirty state when loading a new recipe
                this.setDirtyState(false);

                // Show the editor
                this.showEditor();

                // Fold frontmatter if present
                editor.operation(() => {
                    for (let i = 0; i < editor.lineCount(); i++) {
                        if (editor.getLine(i) === '---') {
                            editor.foldCode(CodeMirror.Pos(i, 0));
                            break;
                        }
                    }
                });
            } catch (error) {
                console.error('Error loading recipe content:', error);
                this.showToast('error', 'Failed to load recipe content');
            }
        },

        createNewRecipe() {
            // Check if current document is dirty before creating a new one
            if (this.dirty) {
                if (!confirm('You have unsaved changes. Do you want to discard them and create a new recipe?')) {
                    return; // User cancelled, stay on current recipe
                }
            }

            // Add placeholder recipe at the top of the list
            this.placeholderRecipe = {
                slug: 'new-untitled-recipe',
                title: 'Untitled Recipe',
                description: 'Currently editing...',
                isPlaceholder: true
            };

            this.currentSlug = null;
            this.originalContent = "";
            editor.setValue('');

            // Reset dirty state for new recipe
            this.setDirtyState(false);
            document.title = 'Untitled Recipe – Recipe Box';

            // Show the editor
            this.showEditor();

            // Make sure to refresh the UI to show the placeholder
            Alpine.nextTick(() => {
                // Select the placeholder recipe in the UI
                document.querySelectorAll('.recipe').forEach(el => el.classList.remove('active'));
                const placeholderEl = document.querySelector(`.recipe[data-slug="new-untitled-recipe"]`);
                if (placeholderEl) {
                    placeholderEl.classList.add('active');
                    placeholderEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            });

            editor.focus();
        },

        showEditor() {
            if (editorContainer) {
                editorContainer.style.display = 'block';
                this.editorVisible = true;

                // Refresh the editor to ensure proper rendering after becoming visible
                setTimeout(() => {
                    editor.refresh();
                }, 10);
            }
        },

        async handleImport() {
            const recipeUrl = prompt('Enter the URL of the recipe you want to import:');

            if (!recipeUrl) {
                return;
            }

            try {
                const response = await fetch('/recipes/import', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ url: recipeUrl })
                });

                if (!response.ok) throw new Error('Failed to import recipe');

                const importedRecipeContent = await response.text();

                // Extract title from the recipe content
                const titleMatch = importedRecipeContent.match(/^=\s*(.+)$/m);
                const recipeTitle = titleMatch ? titleMatch[1] : 'Imported Recipe';

                // Check if current document is dirty before creating a new one
                if (this.dirty) {
                    if (!confirm('You have unsaved changes. Do you want to discard them and import a new recipe?')) {
                        return; // User cancelled, stay on current recipe
                    }
                }

                // Add placeholder recipe at the top of the list
                this.placeholderRecipe = {
                    slug: 'imported-recipe',
                    title: recipeTitle,
                    description: 'Currently editing...',
                    isPlaceholder: true
                };

                this.currentSlug = null;
                this.originalContent = "";

                // Set the editor content to the imported recipe
                editor.setValue(importedRecipeContent);

                // Mark as dirty since this is a new unsaved recipe
                this.setDirtyState(true);

                // Show the editor
                this.showEditor();

                // Make sure to refresh the UI to show the placeholder
                Alpine.nextTick(() => {
                    // Select the placeholder recipe in the UI
                    document.querySelectorAll('.recipe').forEach(el => el.classList.remove('active'));
                    const placeholderEl = document.querySelector(`.recipe[data-slug="imported-recipe"]`);
                    if (placeholderEl) {
                        placeholderEl.classList.add('active');
                        placeholderEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                });

                editor.focus();

                this.showToast('success', 'Recipe imported successfully. Make any edits and save.');
            } catch (error) {
                console.error('Error importing recipe:', error);
                this.showToast('error', 'Failed to import recipe');
            }
        },

        async handleSave() {
            const currentContent = editor.getValue();

            if (!this.currentSlug) {
                await this.createRecipe();
                return;
            }

            if (currentContent === this.originalContent) {
                return;
            }

            if (await this.updateRecipe(this.currentSlug, currentContent)) {
                this.originalContent = currentContent;
                // Reset dirty state after successful save
                this.setDirtyState(false);
            }
        },

        async handlePrintPDF() {
            if (!this.currentSlug) {
                this.showToast('error', 'Please save the recipe first before generating PDF');
                return;
            }

            try {
                const response = await fetch(`/recipes/${encodeURIComponent(this.currentSlug)}/pdf`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    }
                });

                if (!response.ok) throw new Error('Failed to generate PDF');

                const data = await response.json();

                if (data.success && data.pdfData) {
                    if ('showSaveFilePicker' in window) {
                        try {
                            // Create suggested filename from recipe slug
                            const filename = `${this.currentSlug.replace(/-/g, '_')}.pdf`;

                            // Prompt user to select where to save the file
                            const fileHandle = await window.showSaveFilePicker({
                                suggestedName: filename,
                                types: [{
                                    description: 'PDF Document',
                                    accept: {'application/pdf': ['.pdf']}
                                }]
                            });

                            // Create a writable stream and write the PDF data to it
                            const writable = await fileHandle.createWritable();
                            const blob = this.base64ToBlob(data.pdfData, 'application/pdf');
                            await writable.write(blob);
                            await writable.close();

                            this.showToast('success', 'PDF saved successfully');
                            return;
                        } catch (err) {
                            // User may have cancelled the save dialog
                            if (err.name !== 'AbortError') {
                                console.error('Error using File System Access API:', err);
                            }
                            // Fall back to default download if there was an error
                        }
                    }

                    // Fallback for browsers that don't support File System Access API
                    const blob = this.base64ToBlob(data.pdfData, 'application/pdf');
                    const url = URL.createObjectURL(blob);

                    // Create suggested filename from recipe slug
                    const filename = `${this.currentSlug.replace(/-/g, '_')}.pdf`;

                    // Create and trigger download link
                    const a = document.createElement('a');
                    a.href = url;
                    a.download = filename;
                    document.body.appendChild(a);
                    a.click();

                    // Clean up
                    setTimeout(() => {
                        document.body.removeChild(a);
                        URL.revokeObjectURL(url);
                    }, 100);

                    this.showToast('success', 'PDF downloaded successfully');
                } else {
                    throw new Error('Invalid PDF data received');
                }
            } catch (error) {
                console.error('Error generating PDF:', error);
                this.showToast('error', 'Failed to generate PDF');
            }
        },

        base64ToBlob(base64, type = 'application/pdf') {
            const binaryString = window.atob(base64);
            const bytes = new Uint8Array(binaryString.length);

            for (let i = 0; i < binaryString.length; i++) {
                bytes[i] = binaryString.charCodeAt(i);
            }

            return new Blob([bytes], { type });
        },

        async createRecipe() {
            const content = editor.getValue();
            try {
                // Remove the placeholder before making the API call
                this.placeholderRecipe = null;

                const response = await fetch('/recipes', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'text/plain'
                    },
                    body: content
                });

                if (!response.ok) throw new Error('Failed to create recipe');

                const recipe = await response.json();

                this.currentSlug = recipe.slug;
                this.originalContent = content;

                // Reset dirty state after successful creation
                this.setDirtyState(false);

                await this.loadRecipes();

                // Find and highlight the new recipe after refresh
                Alpine.nextTick(() => {
                    const newRecipeLink = document.querySelector(`.recipe[data-slug="${recipe.slug}"]`);
                    if (newRecipeLink) {
                        document.querySelectorAll('.recipe').forEach(el => el.classList.remove('active'));
                        newRecipeLink.classList.add('active');
                        newRecipeLink.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                });

                this.showToast('success', 'New recipe created successfully.');
                return true;
            } catch (error) {
                console.error('Error creating recipe:', error);
                this.showToast('error', 'Failed to create recipe.');
                // Restore placeholder if creation fails
                if (!this.placeholderRecipe) {
                    this.placeholderRecipe = {
                        slug: 'new-untitled-recipe',
                        title: 'Untitled Recipe',
                        description: 'Currently editing...',
                        isPlaceholder: true
                    };
                }
                return false;
            }
        },

        async updateRecipe(slug, content) {
            try {
                const response = await fetch(`/recipes/${encodeURIComponent(slug)}`, {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'text/plain'
                    },
                    body: content
                });

                if (response.ok) {
                    this.showToast('success', 'Recipe saved successfully.');

                    // Reset dirty state after successful save
                    this.setDirtyState(false);

                    // Refresh the recipes list to update categories/positions
                    await this.loadRecipes();

                    // Re-select the current recipe after refresh
                    Alpine.nextTick(() => {
                        // Find and highlight the current recipe
                        document.querySelectorAll('.recipe').forEach(el => el.classList.remove('active'));
                        const currentRecipeLink = document.querySelector(`.recipe[data-slug="${slug}"]`);
                        if (currentRecipeLink) {
                            currentRecipeLink.classList.add('active');
                            // Only scroll into view if it's not currently visible
                            const rect = currentRecipeLink.getBoundingClientRect();
                            const isVisible = (
                                rect.top >= 0 &&
                                rect.left >= 0 &&
                                rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
                                rect.right <= (window.innerWidth || document.documentElement.clientWidth)
                            );
                            if (!isVisible) {
                                currentRecipeLink.scrollIntoView({ behavior: 'smooth', block: 'center' });
                            }
                        }
                    });

                    return true;
                } else {
                    this.showToast('error', 'Failed to save recipe.');
                    return false;
                }
            } catch (error) {
                console.error('Error:', error);
                this.showToast('error', 'Failed to save recipe.');
                return false;
            }
        },

        async deleteRecipe(slug) {
            if (!slug) return;

            try {
                const response = await fetch(`/recipes/${encodeURIComponent(slug)}`, {
                    method: 'DELETE'
                });

                if (!response.ok) throw new Error('Failed to delete recipe');

                this.showToast('success', 'Recipe deleted successfully.');

                // Clear the current slug and editor if we deleted the active recipe
                if (this.currentSlug === slug) {
                    this.currentSlug = null;
                    this.originalContent = "";
                    editor.setValue('');
                    document.title = 'Recipe Box';

                    // Reset dirty state after deletion
                    this.setDirtyState(false);

                    // Hide the editor instead of creating a new recipe
                    if (editorContainer) {
                        editorContainer.style.display = 'none';
                        this.editorVisible = false;
                    }

                    // Make sure no placeholder is shown
                    this.placeholderRecipe = null;
                }

                await this.loadRecipes();
            } catch (error) {
                console.error('Error deleting recipe:', error);
                this.showToast('error', 'Failed to delete recipe.');
            }
        },

        showToast(status, message) {
            this.toast.message = message;
            this.toast.status = status;
            this.toast.visible = true;

            setTimeout(() => {
                this.toast.visible = false;
                this.toast.status = '';
            }, 3000);
        },

        isRecipeVisible(recipe) {
            if (!this.searchQuery) return true;

            return recipe.title.toLowerCase().includes(this.searchQuery.toLowerCase());
        },

        isSubcategoryVisible(category, subcategoryName) {
            if (!this.searchQuery) return true;

            // Check if any recipe in this subcategory is visible
            const subcategoryRecipes = this.getSubcategoryRecipes(category, subcategoryName);
            return subcategoryRecipes.some(recipe => this.isRecipeVisible(recipe));
        },

        isCategoryVisible(categoryName) {
            if (!this.searchQuery) return true;

            // Check direct recipes
            const directRecipes = this.getCategoryRecipes(categoryName, null);
            const hasVisibleDirectRecipes = directRecipes.some(recipe => this.isRecipeVisible(recipe));

            // Check subcategories
            const subcategories = this.getCategorySubcategories(categoryName);
            const hasVisibleSubcategories = subcategories.some(subcategory =>
                this.isSubcategoryVisible(categoryName, subcategory)
            );

            return hasVisibleDirectRecipes || hasVisibleSubcategories;
        },

        markError(line, message) {
            // Clear any existing error markers
            this.clearErrors();

            // Get the line information
            const lineStart = {line: line, ch: 0};
            const lineEnd = {line: line, ch: editor.getLine(line).length};

            // Create the marker with appropriate styling
            const marker = editor.markText(lineStart, lineEnd, {
                className: 'error-line',
                title: message
            });

            // Store the marker reference so we can clear it later
            editor._parsingErrorMarker = marker;
        },

        // Clear existing parsing errors
        clearErrors() {
            // Clear existing error markers
            if (editor._parsingErrorMarker) {
                editor._parsingErrorMarker.clear();
                editor._parsingErrorMarker = null;
            }
        },

        async validateContent() {
            try {
                const content = editor.getValue();
                const response = await fetch('/recipes/validate', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'text/plain'
                    },
                    body: content
                });

                if (response.status === 204) {
                    // Success - no validation errors
                    this.clearErrors();
                } else if (response.status === 400) {
                    // Validation error
                    const errorData = await response.json();
                    this.markError(errorData.line - 1, errorData.message);
                }
            } catch (error) {
                console.error('Error validating content:', error);
            }
        },

        debounce(func, wait) {
            let timeout;
            return function() {
                const context = this;
                const args = arguments;
                clearTimeout(timeout);
                timeout = setTimeout(() => func.apply(context, args), wait);
            };
        },

        // Helper methods for recipe organization
        getCategories() {
            const categories = new Set();

            Object.values(this.recipes).forEach(recipe => {
                categories.add(recipe.meta?.category || 'Uncategorized');
            });

            return Array.from(categories).sort();
        },

        getCategorySubcategories(category) {
            const subcategories = new Set();

            Object.values(this.recipes).forEach(recipe => {
                if ((recipe.meta?.category || 'Uncategorized') === category && recipe.meta?.subcategory) {
                    subcategories.add(recipe.meta.subcategory);
                }
            });

            return Array.from(subcategories).sort();
        },

        getCategoryRecipes(category, subcategory) {
            // Get all recipes from this.recipes
            let recipesList = Object.entries(this.recipes)
                .filter(([slug, recipe]) => {
                    const recipeCategory = recipe.meta?.category || 'Uncategorized';
                    const recipeSubcategory = recipe.meta?.subcategory || '';

                    if (subcategory === null) {
                        // Direct category recipes (no subcategory)
                        return recipeCategory === category && !recipeSubcategory;
                    } else {
                        // Subcategory recipes
                        return recipeCategory === category && recipeSubcategory === subcategory;
                    }
                })
                .map(([slug, recipe]) => ({
                    slug,
                    title: recipe.title || 'Untitled Recipe',
                    description: recipe.description || ''
                }));

            // Add placeholder to uncategorized if it exists
            if (this.placeholderRecipe && category === 'Uncategorized' && subcategory === null) {
                recipesList = [this.placeholderRecipe, ...recipesList];
            }

            return recipesList.sort((a, b) => a.title.localeCompare(b.title));
        },

        getUncategorizedRecipes() {
            // Get standard uncategorized recipes
            let uncategorizedList = Object.entries(this.recipes)
                .filter(([slug, recipe]) => {
                    return !recipe.meta?.category || recipe.meta.category === 'Uncategorized';
                })
                .map(([slug, recipe]) => ({
                    slug,
                    title: recipe.title || 'Untitled Recipe',
                    description: recipe.description || ''
                }));

            // Add placeholder to the top if it exists
            if (this.placeholderRecipe) {
                uncategorizedList = [this.placeholderRecipe, ...uncategorizedList];
            }

            return uncategorizedList.sort((a, b) => a.title.localeCompare(b.title));
        },
        
        getSubcategoryRecipes(category, subcategory) {
            return this.getCategoryRecipes(category, subcategory);
        }
    });
});