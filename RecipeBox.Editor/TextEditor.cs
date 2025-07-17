using System.Text;

namespace RecipeBox.Editor;

public class TextEditor
{
    private readonly string _filePath;
    private int _cursorCol;
    private int _cursorRow;
    private readonly List<StringBuilder> _lines;
    private bool _needsRedraw = true;
    private string _statusMessage = "Ready";
    private int _viewTopRow;

    public TextEditor(string filePath)
    {
        _filePath = filePath;
        _cursorRow = 0;
        _cursorCol = 0;
        _viewTopRow = 0;

        if (File.Exists(_filePath))
        {
            var fileLines = File.ReadAllLines(_filePath);
            _lines = fileLines.Select(line => new StringBuilder(line)).ToList();
            if (_lines.Count == 0)
            {
                _lines.Add(new StringBuilder());
            }
        }
        else
        {
            _lines = [new StringBuilder()];
        }
    }

    public string Text => string.Join(Environment.NewLine, _lines.Select(l => l.ToString()));
    public Func<string, (bool IsValid, string ErrorMessage)>? Validator { get; set; }

    public bool Run()
    {
        Console.CursorVisible = true;
        Console.Clear();

        var running = true;
        var savedAndExited = false;

        while (running)
        {
            if (_needsRedraw)
            {
                Render();
                _needsRedraw = false;
            }

            PositionCursor();

            var keyInfo = Console.ReadKey(true);

            if (_statusMessage.StartsWith("Error:"))
            {
                _statusMessage = "Ready";
            }

            switch (keyInfo.Key)
            {
                case ConsoleKey.S when keyInfo.Modifiers == ConsoleModifiers.Control:
                    var (isValid, errorMessage) = Validator?.Invoke(Text) ?? (true, string.Empty);
                    if (isValid)
                    {
                        SaveFile();
                        _statusMessage = "File saved successfully.";
                        savedAndExited = true;
                        running = false;
                    }
                    else
                    {
                        _statusMessage = $"Error: {errorMessage}";
                    }

                    break;

                case ConsoleKey.Q when keyInfo.Modifiers == ConsoleModifiers.Control:
                    running = false;
                    break;

                case ConsoleKey.UpArrow:
                    MoveCursorUp();
                    break;
                case ConsoleKey.DownArrow:
                    MoveCursorDown();
                    break;
                case ConsoleKey.LeftArrow:
                    MoveCursorLeft();
                    break;
                case ConsoleKey.RightArrow:
                    MoveCursorRight();
                    break;
                case ConsoleKey.Home:
                    _cursorCol = 0;
                    break;
                case ConsoleKey.End:
                    _cursorCol = _lines[_cursorRow].Length;
                    break;
                case ConsoleKey.Enter:
                    InsertNewLine();
                    break;
                case ConsoleKey.Backspace:
                    HandleBackspace();
                    break;
                case ConsoleKey.Delete:
                    HandleDelete();
                    break;
                default:
                    if (!char.IsControl(keyInfo.KeyChar))
                    {
                        InsertChar(keyInfo.KeyChar);
                    }

                    break;
            }

            _needsRedraw = true;
        }

        Console.Clear();
        Console.CursorVisible = true;
        Console.SetCursorPosition(0, 0);
        return savedAndExited;
    }

    private void Render()
    {
        var width = Console.WindowWidth;
        var height = Console.WindowHeight;
        var editorHeight = height - 2;

        Console.SetCursorPosition(0, 0);
        DrawHeader(width);

        var (displayLines, lineMap) = GetDisplayLinesAndMap(width);
        Scroll(displayLines, editorHeight);

        for (var i = 0; i < editorHeight; i++)
        {
            var displayIndex = _viewTopRow + i;
            Console.SetCursorPosition(0, i + 1);

            if (displayIndex >= displayLines.Count)
            {
                Console.Write("".PadRight(width));
                continue;
            }

            var lineToDraw = displayLines[displayIndex];
            var originalLineIndex = lineMap[displayIndex];
            var originalLine = _lines[originalLineIndex].ToString();

            DrawSyntaxHighlightedLine(originalLine, lineToDraw, lineMap.IndexOf(originalLineIndex) == displayIndex,
                width);
        }

        DrawFooter(width, height);
    }

    private void DrawSyntaxHighlightedLine(string originalLine, string lineToDraw, bool isFirstChunk, int width)
    {
        Console.ResetColor();
        var prefix = originalLine.Length >= 2 ? originalLine.Substring(0, 2) : "";

        var textColor = ConsoleColor.Gray;
        if (prefix == "= ")
        {
            textColor = ConsoleColor.Yellow;
        }
        else if (prefix == "+ ")
        {
            textColor = ConsoleColor.Red;
        }
        else if (prefix == "# ")
        {
            textColor = ConsoleColor.White;
        }
        else if (prefix == "- ")
        {
            textColor = ConsoleColor.Gray;
        }

        if (isFirstChunk && (prefix == "= " || prefix == "+ " || prefix == "# " || prefix == "- "))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(originalLine[0]);
            Console.ForegroundColor = textColor;
            Console.Write(lineToDraw.Length > 1 ? lineToDraw.Substring(1) : "");
        }
        else
        {
            Console.ForegroundColor = textColor;
            Console.Write(lineToDraw);
        }

        var writtenLength = lineToDraw.Length;
        Console.Write("".PadRight(width - writtenLength));
    }

    private (List<string> displayLines, List<int> lineMap) GetDisplayLinesAndMap(int width)
    {
        var displayLines = new List<string>();
        var lineMap = new List<int>();
        for (var i = 0; i < _lines.Count; i++)
        {
            var lineStr = _lines[i].ToString();
            if (lineStr.Length == 0)
            {
                displayLines.Add("");
                lineMap.Add(i);
                continue;
            }

            for (var j = 0; j < lineStr.Length; j += width)
            {
                displayLines.Add(lineStr.Substring(j, Math.Min(width, lineStr.Length - j)));
                lineMap.Add(i);
            }
        }

        return (displayLines, lineMap);
    }

    private (int row, int col) GetDisplayPosition(int width)
    {
        var displayRow = 0;
        for (var i = 0; i < _cursorRow; i++)
        {
            var lineLength = _lines[i].Length;
            if (lineLength == 0)
            {
                displayRow++;
            }
            else
            {
                displayRow += (lineLength - 1) / width + 1;
            }
        }

        displayRow += _cursorCol / width;
        var displayCol = _cursorCol % width;
        return (displayRow, displayCol);
    }

    private void Scroll(List<string> displayLines, int editorHeight)
    {
        var (displayRow, _) = GetDisplayPosition(Console.WindowWidth);

        if (displayRow < _viewTopRow)
        {
            _viewTopRow = displayRow;
        }

        if (displayRow >= _viewTopRow + editorHeight)
        {
            _viewTopRow = displayRow - editorHeight + 1;
        }
    }

    private void PositionCursor()
    {
        var (displayRow, displayCol) = GetDisplayPosition(Console.WindowWidth);
        Console.SetCursorPosition(displayCol, displayRow - _viewTopRow + 1);
    }

    private void DrawHeader(int width)
    {
        var headerText = "CTRL-S: Save & Exit | CTRL-Q: Exit w/o Saving";
        Console.BackgroundColor = ConsoleColor.White;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Write(headerText.PadRight(width));
        Console.ResetColor();
    }

    private void DrawFooter(int width, int height)
    {
        Console.SetCursorPosition(0, height - 1);
        Console.BackgroundColor = ConsoleColor.White;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Write(_statusMessage.PadRight(width));
        Console.ResetColor();
    }

    private void SaveFile()
    {
        File.WriteAllText(_filePath, Text);
    }

    private void MoveCursorUp()
    {
        if (_cursorRow > 0)
        {
            _cursorRow--;
            _cursorCol = Math.Min(_cursorCol, _lines[_cursorRow].Length);
        }
    }

    private void MoveCursorDown()
    {
        if (_cursorRow < _lines.Count - 1)
        {
            _cursorRow++;
            _cursorCol = Math.Min(_cursorCol, _lines[_cursorRow].Length);
        }
    }

    private void MoveCursorLeft()
    {
        if (_cursorCol > 0)
        {
            _cursorCol--;
        }
        else if (_cursorRow > 0)
        {
            _cursorRow--;
            _cursorCol = _lines[_cursorRow].Length;
        }
    }

    private void MoveCursorRight()
    {
        if (_cursorCol < _lines[_cursorRow].Length)
        {
            _cursorCol++;
        }
        else if (_cursorRow < _lines.Count - 1)
        {
            _cursorRow++;
            _cursorCol = 0;
        }
    }

    private void InsertChar(char c)
    {
        _lines[_cursorRow].Insert(_cursorCol, c);
        _cursorCol++;
    }

    private void InsertNewLine()
    {
        var currentLine = _lines[_cursorRow];
        var restOfLine = currentLine.ToString().Substring(_cursorCol);
        currentLine.Remove(_cursorCol, currentLine.Length - _cursorCol);
        _lines.Insert(_cursorRow + 1, new StringBuilder(restOfLine));
        _cursorRow++;
        _cursorCol = 0;
    }

    private void HandleBackspace()
    {
        if (_cursorCol > 0)
        {
            _cursorCol--;
            _lines[_cursorRow].Remove(_cursorCol, 1);
        }
        else if (_cursorRow > 0)
        {
            var lineToMerge = _lines[_cursorRow].ToString();
            _lines.RemoveAt(_cursorRow);
            _cursorRow--;
            _cursorCol = _lines[_cursorRow].Length;
            _lines[_cursorRow].Append(lineToMerge);
        }
    }

    private void HandleDelete()
    {
        if (_cursorCol < _lines[_cursorRow].Length)
        {
            _lines[_cursorRow].Remove(_cursorCol, 1);
        }
        else if (_cursorRow < _lines.Count - 1)
        {
            var nextLine = _lines[_cursorRow + 1].ToString();
            _lines.RemoveAt(_cursorRow + 1);
            _lines[_cursorRow].Append(nextLine);
        }
    }
}