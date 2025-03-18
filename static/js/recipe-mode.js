// recipe-mode.js - CodeMirror custom mode for recipe format
CodeMirror.defineMode("recipe", function(config) {
    const SINGLE_QUANTITY = /(?:\d+\/\d+|\d+(?:\s+\d+\/\d+)?)/;
    const RANGE_QUANTITY = new RegExp(`(?:${SINGLE_QUANTITY.source})-(?:${SINGLE_QUANTITY.source})`);
    const UNITS = /(?:cups?|teaspoons?|tablespoons?|tsp|tbsp|ounces?|pounds?|quarts?|ml|milliliters?|grams?|cans?|each|cloves?|stalks?|bunch|bunches|sprigs?|bags?|pinch|sticks?)/i;

    return {
        startState: function() {
            return {
                inFrontmatter: false,
                linePrefix: null,
                ingredientPart: null,
                inAltAmount: false,
                firstIngredientWord: false // Flag to track the first word of ingredient
            };
        },
        token: function(stream, state) {
            // Start of line processing
            if (stream.sol()) {
                state.linePrefix = null;
                state.ingredientPart = null;
                state.inAltAmount = false;
                state.firstIngredientWord = false;

                if (stream.match(/^---$/)) {
                    state.inFrontmatter = !state.inFrontmatter;
                    return "recipe-frontmatter";
                }

                if (stream.match(/^[=+*>#]/)) {
                    state.linePrefix = stream.string.charAt(stream.pos - 1);
                    stream.eatSpace();
                    return "recipe-prefix";
                }
            }

            // Frontmatter handling
            if (state.inFrontmatter) {
                stream.skipToEnd();
                return "recipe-frontmatter";
            }

            // Line prefix-based processing
            switch (state.linePrefix) {
                case '=':
                    stream.skipToEnd();
                    return "recipe-title";

                case '+':
                    stream.skipToEnd();
                    return "recipe-subtitle";

                case '#':
                    stream.skipToEnd();
                    return "recipe-step";

                case '>':
                    stream.skipToEnd();
                    return "recipe-description";

                case '*':
                    stream.skipToEnd();
                    return "recipe-ingredient";
//                    // Ingredient line processing
//
//                    // Notes part (after comma)
//                    if (stream.peek() === ',') {
//                        stream.next(); // consume comma
//                        stream.skipToEnd();
//                        return "recipe-notes";
//                    }
//
//                    // Alt amount handling (inside parentheses)
//                    if (state.inAltAmount) {
//                        // Opening parenthesis of alt amount
//                        if (state.ingredientPart === 'alt_start') {
//                            state.ingredientPart = 'alt_quantity_pending';
//                            // The opening parenthesis was already consumed when entering alt_amount state
//                            return "recipe-alt-amount";
//                        }
//
//                        // End of alt amount
//                        if (stream.peek() === ')') {
//                            stream.next();
//                            state.inAltAmount = false;
//                            state.ingredientPart = 'ingredient';
//                            return "recipe-alt-amount";
//                        }
//
//                        // Quantity within alt amount
//                        if (state.ingredientPart === 'alt_quantity_pending' &&
//                            (stream.match(RANGE_QUANTITY) || stream.match(SINGLE_QUANTITY))) {
//                            state.ingredientPart = 'alt_unit_pending';
//                            return "recipe-alt-quantity";
//                        }
//
//                        // Unit within alt amount
//                        if (state.ingredientPart === 'alt_unit_pending' && stream.eatSpace() && stream.match(UNITS)) {
//                            state.ingredientPart = 'alt_complete';
//                            return "recipe-alt-unit";
//                        }
//
//                        // Any other character within alt amount
//                        stream.next();
//                        return "recipe-alt-amount";
//                    }
//
//                    // Start of line - quantity
//                    if (!state.ingredientPart) {
//                        if (stream.match(RANGE_QUANTITY) || stream.match(SINGLE_QUANTITY)) {
//                            state.ingredientPart = 'quantity';
//                            return "recipe-quantity";
//                        } else {
//                            // If no quantity, mark that we need to handle the first word as ingredient
//                            state.ingredientPart = 'ingredient';
//                            state.firstIngredientWord = true;
//                        }
//                    }
//
//                    // After quantity - unit
//                    if (state.ingredientPart === 'quantity' && stream.eatSpace() && stream.match(UNITS)) {
//                        state.ingredientPart = 'unit';
//                        return "recipe-unit";
//                    }
//
//                    // After unit - check for alt amount
//                    if (state.ingredientPart === 'unit' && stream.eatSpace() && stream.peek() === '(') {
//                        stream.next();
//                        state.inAltAmount = true;
//                        state.ingredientPart = 'alt_start';
//                        return "recipe-alt-amount";
//                    }
//
//                    // Spaces between parts
//                    if (stream.eatSpace()) {
//                        // After consuming space, if we're in the ingredient part and have not
//                        // handled the first word yet, we need to mark it ready to handle
//                        if (state.ingredientPart === 'ingredient' && !state.firstIngredientWord) {
//                            // This is for space after unit or quantity
//                            return null;
//                        }
//                        return null;
//                    }
//
//                    // Ingredient name handling
//                    if (state.ingredientPart === 'ingredient' || state.ingredientPart === 'unit') {
//                        // Special handling for the first word when no quantity/unit
//                        if (state.ingredientPart === 'ingredient' && state.firstIngredientWord) {
//                            // Consume the first ingredient word
//                            while (stream.peek() && !stream.peek().match(/[\s,]/) && !stream.eol()) {
//                                stream.next();
//                            }
//                            state.firstIngredientWord = false; // We've handled the first word
//                            return "recipe-ingredient";
//                        }
//
//                        // For cases after a unit or for subsequent ingredient words
//                        if (stream.peek() !== ',') {
//                            // Consume until next space, comma, or EOL
//                            while (stream.peek() && !stream.peek().match(/[\s,]/) && !stream.eol()) {
//                                stream.next();
//                            }
//                            return "recipe-ingredient";
//                        }
//                    }
//
//                    // Default case for ingredient line: advance and return null
//                    stream.next();
//                    return null;
            }

            // Default for any other case: advance and return null
            stream.next();
            return null;
        }
    };
});

// Folding helper for recipe frontmatter
CodeMirror.registerHelper("fold", "recipe", function(cm, start) {
    const lineText = cm.getLine(start.line);
    if (lineText !== "---") return null;
    const lastLine = cm.lastLine();

    for (let i = start.line + 1; i <= lastLine; i++) {
        if (cm.getLine(i) === "---") {
            return {
                from: CodeMirror.Pos(start.line, 0),
                to: CodeMirror.Pos(i, 3)
            };
        }
    }
    return null;
});