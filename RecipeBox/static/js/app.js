let editor;
let editorContainer;

document.addEventListener('DOMContentLoaded', function() {
    editorContainer = document.querySelector('.editor-container') || document.getElementById('editor').parentElement;

    if (editorContainer) editorContainer.style.display = 'none';

    editor = CodeMirror.fromTextArea(document.getElementById('editor'), {
        mode: 'recipe',
        lineNumbers: false,
        lineWrapping: true,
        foldGutter: true,
        foldOptions: { widget: "---" },
        gutters: ['CodeMirror-foldgutter'],
        indentUnit: 4
    });

    editor.on('change', function() {
        const store = window.Alpine.store('recipeStore');
        store.setDirtyState(editor.getValue() !== store.originalContent);
        store.debouncedValidate();
    });
});

document.addEventListener('alpine:init', () => {
    Alpine.store('recipeStore', {
        currentSlug: null,
        originalContent: "",
        recipes: {},
        dirty: false,
        searchMode: false,
        searchQuery: '',
        editorVisible: false,
        placeholderRecipe: null,
        toast: {
            message: '',
            status: '',
            visible: false
        },

        init() {
            this.debouncedValidate = this.debounce(this.validateContent, 100);

            document.addEventListener('contextmenu', this.handleContextMenu.bind(this));
            document.addEventListener('click', () => document.getElementById('recipeContextMenu').style.display = 'none');
            document.getElementById('recipeContextMenu').addEventListener('click', this.handleContextMenuAction.bind(this));
            document.addEventListener('keydown', this.handleKeyboardShortcuts.bind(this));
            window.addEventListener('beforeunload', this.handleBeforeUnload.bind(this));

            this.loadRecipes();
        },

        handleKeyboardShortcuts(e) {
            if ((e.ctrlKey || e.metaKey) && e.key === 's') {
                e.preventDefault();
                this.handleSave();
                return false;
            }
            if ((e.ctrlKey || e.metaKey) && e.key === 'p') {
                e.preventDefault();
                this.handlePrintPDF();
                return false;
            }
            if ((e.ctrlKey || e.metaKey) && e.key === 'i') {
                e.preventDefault();
                this.handleImport();
                return false;
            }
        },

        handleBeforeUnload(e) {
            navigator.sendBeacon('/shutdown');

            if (this.dirty) {
                e.preventDefault();
                e.returnValue = 'You have unsaved changes. Are you sure you want to leave?';
                return e.returnValue;
            }
        },

        handleContextMenu(e) {
            const recipeElement = e.target.closest('.recipe');
            if (!recipeElement) return;

            e.preventDefault();
            const slug = recipeElement.dataset.slug;

            const contextMenu = document.getElementById('recipeContextMenu');
            contextMenu.style.left = e.pageX + 'px';
            contextMenu.style.top = e.pageY + 'px';
            contextMenu.style.display = 'block';
            contextMenu.dataset.slug = slug;
        },

        handleContextMenuAction(e) {
            const action = e.target.dataset.action;
            const slug = e.currentTarget.dataset.slug;

            if (action === 'delete' && slug && confirm('Are you sure you want to delete this recipe?')) {
                this.deleteRecipe(slug);
            }

            e.currentTarget.style.display = 'none';
        },

        async changeRecipe(options = {}) {
            const { slug, content, isNew, isImport, skipDirtyCheck } = options;

            if (!skipDirtyCheck && this.dirty &&
                !confirm('You have unsaved changes. Do you want to discard them?')) {
                return false;
            }

            this.placeholderRecipe = null;

            if (isNew || isImport) {
                const title = isImport && content ? this.extractTitle(content) : 'Untitled Recipe';
                this.placeholderRecipe = {
                    slug: isImport ? 'imported-recipe' : 'new-untitled-recipe',
                    title,
                    description: 'Currently editing...',
                    isPlaceholder: true
                };
            }

            this.currentSlug = slug || null;
            this.originalContent = isNew ? '' : (content || '');
            editor.setValue(content || '');

            this.setDirtyState(isImport);
            this.showEditor();

            editor.focus();
            this.foldFrontmatter();

            const selectSlug = isNew || isImport ?
                this.placeholderRecipe.slug : slug;
            this.selectRecipeInUI(selectSlug);

            return true;
        },

        foldFrontmatter() {
            editor.operation(() => {
                for (let i = 0; i < editor.lineCount(); i++) {
                    if (editor.getLine(i) === '---') {
                        editor.foldCode(CodeMirror.Pos(i, 0));
                        break;
                    }
                }
            });
        },

        extractTitle(content) {
            const titleMatch = content.match(/^=\s*(.+)$/m);
            return titleMatch ? titleMatch[1] : 'Imported Recipe';
        },

        async safeApiCall(apiFunc, successMessage, errorMessage) {
            try {
                const result = await apiFunc();
                if (successMessage) this.showToast('success', successMessage);
                return result;
            } catch (error) {
                console.error(`API Error: ${errorMessage}`, error);
                this.showToast('error', errorMessage);
                return null;
            }
        },

        async loadRecipes() {
            return this.safeApiCall(
                async () => {
                    const response = await fetch('/recipes');
                    if (!response.ok) throw new Error('Failed to load recipes');
                    this.recipes = await response.json();
                    return this.recipes;
                },
                null,
                'Failed to load recipes'
            );
        },

        async loadRecipeContent(slug) {
            return this.safeApiCall(
                async () => {
                    const response = await fetch(`/recipes/${encodeURIComponent(slug)}`);
                    if (!response.ok) throw new Error('Failed to load recipe content');
                    const text = await response.text();
                    return this.changeRecipe({ slug, content: text });
                },
                null,
                'Failed to load recipe content'
            );
        },

        async createRecipe() {
            const content = editor.getValue();
            return this.safeApiCall(
                async () => {
                    this.placeholderRecipe = null;

                    const response = await fetch('/recipes', {
                        method: 'POST',
                        headers: { 'Content-Type': 'text/plain' },
                        body: content
                    });

                    if (!response.ok) throw new Error('Failed to create recipe');
                    const recipe = await response.json();

                    this.currentSlug = recipe.slug;
                    this.originalContent = content;
                    this.setDirtyState(false);

                    await this.loadRecipes();
                    this.selectRecipeInUI(recipe.slug);

                    return recipe;
                },
                'New recipe created successfully.',
                'Failed to create recipe.'
            );
        },

        async updateRecipe(slug, content) {
            return this.safeApiCall(
                async () => {
                    const response = await fetch(`/recipes/${encodeURIComponent(slug)}`, {
                        method: 'PUT',
                        headers: { 'Content-Type': 'text/plain' },
                        body: content
                    });

                    if (!response.ok) throw new Error('Failed to save recipe');

                    this.originalContent = content;
                    this.setDirtyState(false);

                    await this.loadRecipes();
                    this.selectRecipeInUI(slug);

                    return true;
                },
                'Recipe saved successfully.',
                'Failed to save recipe.'
            );
        },

        async deleteRecipe(slug) {
            if (!slug) return;

            return this.safeApiCall(
                async () => {
                    const response = await fetch(`/recipes/${encodeURIComponent(slug)}`, {
                        method: 'DELETE'
                    });

                    if (!response.ok) throw new Error('Failed to delete recipe');

                    if (this.currentSlug === slug) {
                        this.currentSlug = null;
                        this.originalContent = "";
                        this.placeholderRecipe = null;
                        editor.setValue('');
                        this.setDirtyState(false);

                        if (editorContainer) {
                            editorContainer.style.display = 'none';
                            this.editorVisible = false;
                        }
                    }

                    await this.loadRecipes();
                    return true;
                },
                'Recipe deleted successfully.',
                'Failed to delete recipe.'
            );
        },

        async validateContent() {
            try {
                const content = editor.getValue();
                const response = await fetch('/recipes/validate', {
                    method: 'POST',
                    headers: { 'Content-Type': 'text/plain' },
                    body: content
                });

                if (response.status === 204) {
                    this.clearErrors();
                } else if (response.status === 400) {
                    const errorData = await response.json();
                    this.markError(errorData.line - 1, errorData.message);
                }
            } catch (error) {
                console.error('Error validating content:', error);
            }
        },

        createNewRecipe() {
            return this.changeRecipe({ isNew: true });
        },

        async handleImport() {
            const recipeUrl = prompt('Enter the URL of the recipe you want to import:');
            if (!recipeUrl) return;

            return this.safeApiCall(
                async () => {
                    const url = new URL('/recipes/import', window.location.origin);
                    url.searchParams.append('url', recipeUrl);

                    const response = await fetch(url.toString(), {
                        method: 'GET',
                        headers: { 'Accept': 'application/json' }
                    });

                    if (!response.ok) throw new Error('Failed to import recipe');
                    const importedRecipeContent = await response.text();

                    return this.changeRecipe({
                        isImport: true,
                        content: importedRecipeContent
                    });
                },
                'Recipe imported successfully. Make any edits and save.',
                'Failed to import recipe'
            );
        },

        async handleSave() {
            const currentContent = editor.getValue();

            if (!this.currentSlug) {
                return this.createRecipe();
            }

            if (currentContent === this.originalContent) {
                return true;
            }

            return this.updateRecipe(this.currentSlug, currentContent);
        },

        async handlePrintPDF() {
            if (!this.currentSlug) {
                this.showToast('error', 'Please save the recipe first before printing');
                return;
            }

            return this.safeApiCall(
                async () => {
                    const response = await fetch(`/recipes/${encodeURIComponent(this.currentSlug)}/pdf`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' }
                    });

                    if (!response.ok) throw new Error('Failed to generate PDF');
                    const data = await response.json();

                    if (!data.success || !data.pdfData) {
                        throw new Error('Invalid PDF data received');
                    }

                    // Create a blob URL for the PDF
                    const blob = this.base64ToBlob(data.pdfData);
                    const blobUrl = URL.createObjectURL(blob);

                    // Get or create print iframe
                    let printFrame = document.getElementById('print-frame');
                    if (!printFrame) {
                        printFrame = document.createElement('iframe');
                        printFrame.id = 'print-frame';
                        printFrame.style.position = 'fixed';
                        printFrame.style.right = '0';
                        printFrame.style.bottom = '0';
                        printFrame.style.width = '0';
                        printFrame.style.height = '0';
                        printFrame.style.border = '0';
                        document.body.appendChild(printFrame);
                    }

                    // Load the PDF and print it
                    printFrame.onload = () => {
                        // Register afterprint handler (supported in all Chromium browsers)
                        printFrame.contentWindow.addEventListener('afterprint', () => {
                            URL.revokeObjectURL(blobUrl);
                        }, { once: true });

                        // Trigger print dialog
                        printFrame.contentWindow.print();
                    };

                    // Set the iframe source to the PDF blob URL
                    printFrame.src = blobUrl;

                    return true;
                },
                null,
                'Failed to print recipe'
            );
        },
        
        toggleSearchMode(enabled) {
            this.searchMode = enabled;
            this.searchQuery = '';

            if (enabled) {
                Alpine.nextTick(() => {
                    document.querySelector('.search-input')?.focus();
                });
            }
        },

        showEditor() {
            if (editorContainer) {
                editorContainer.style.display = 'block';
                this.editorVisible = true;
                setTimeout(() => editor.refresh(), 10);
            }
        },

        setDirtyState(isDirty) {
            this.dirty = isDirty;
            this.updateWindowTitle();
        },

        updateWindowTitle() {
            if (!this.currentSlug && !this.placeholderRecipe) {
                document.title = 'Recipe Box';
                return;
            }

            const titleMatch = editor.getValue().match(/^=\s*(.+)$/m);
            const baseTitle = titleMatch ? titleMatch[1] : 'Untitled Recipe';
            document.title = `${this.dirty ? '*' : ''}${baseTitle} – Recipe Box`;
        },

        selectRecipeInUI(slug) {
            Alpine.nextTick(() => {
                document.querySelectorAll('.recipe').forEach(el =>
                    el.classList.toggle('active', el.dataset.slug === slug));

                const element = document.querySelector(`.recipe[data-slug="${slug}"]`);
                if (element) {
                    const rect = element.getBoundingClientRect();
                    const isVisible = (
                        rect.top >= 0 &&
                        rect.bottom <= (window.innerHeight || document.documentElement.clientHeight)
                    );

                    if (!isVisible) {
                        element.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                }
            });
        },

        showToast(status, message, duration = 3000) {
            this.toast = { message, status, visible: true };

            if (this._toastTimer) clearTimeout(this._toastTimer);
            this._toastTimer = setTimeout(() => {
                this.toast.visible = false;
                this.toast.status = '';
            }, duration);
        },

        markError(line, message) {
            this.clearErrors();

            const lineStart = {line: line, ch: 0};
            const lineEnd = {line: line, ch: editor.getLine(line).length};

            editor._parsingErrorMarker = editor.markText(lineStart, lineEnd, {
                className: 'error-line',
                title: message
            });
        },

        clearErrors() {
            if (editor._parsingErrorMarker) {
                editor._parsingErrorMarker.clear();
                editor._parsingErrorMarker = null;
            }
        },

        filterRecipes(query) {
            this.searchQuery = query;
        },

        isRecipeVisible(recipe) {
            if (!this.searchQuery) return true;
            return recipe.title.toLowerCase().includes(this.searchQuery.toLowerCase());
        },

        isSubcategoryVisible(category, subcategoryName) {
            if (!this.searchQuery) return true;
            const subcategoryRecipes = this.getSubcategoryRecipes(category, subcategoryName);
            return subcategoryRecipes.some(recipe => this.isRecipeVisible(recipe));
        },

        isCategoryVisible(categoryName) {
            if (!this.searchQuery) return true;

            const directRecipes = this.getCategoryRecipes(categoryName, null);
            const hasVisibleDirectRecipes = directRecipes.some(recipe => this.isRecipeVisible(recipe));

            const subcategories = this.getCategorySubcategories(categoryName);
            const hasVisibleSubcategories = subcategories.some(subcategory =>
                this.isSubcategoryVisible(categoryName, subcategory)
            );

            return hasVisibleDirectRecipes || hasVisibleSubcategories;
        },

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

        getCategoryRecipes(category, subcategory = null) {
            let recipesList = Object.entries(this.recipes)
                .filter(([_, recipe]) => {
                    const recipeCategory = recipe.meta?.category || 'Uncategorized';
                    const recipeSubcategory = recipe.meta?.subcategory || '';

                    if (subcategory === null) {
                        return recipeCategory === category && !recipeSubcategory;
                    } else {
                        return recipeCategory === category && recipeSubcategory === subcategory;
                    }
                })
                .map(([slug, recipe]) => ({
                    slug,
                    title: recipe.title || 'Untitled Recipe',
                    description: recipe.description || ''
                }));

            if (this.placeholderRecipe && category === 'Uncategorized' && subcategory === null) {
                recipesList = [this.placeholderRecipe, ...recipesList];
            }

            return recipesList.sort((a, b) => a.title.localeCompare(b.title));
        },

        getSubcategoryRecipes(category, subcategory) {
            return this.getCategoryRecipes(category, subcategory);
        },

        getUncategorizedRecipes() {
            return this.getCategoryRecipes('Uncategorized');
        },

        base64ToBlob(base64, type = 'application/pdf') {
            const binaryString = window.atob(base64);
            const bytes = new Uint8Array(binaryString.length);
            for (let i = 0; i < binaryString.length; i++) {
                bytes[i] = binaryString.charCodeAt(i);
            }
            return new Blob([bytes], { type });
        },

        debounce(func, wait) {
            let timeout;
            return function() {
                const context = this;
                const args = arguments;
                clearTimeout(timeout);
                timeout = setTimeout(() => func.apply(context, args), wait);
            };
        }
    });
});