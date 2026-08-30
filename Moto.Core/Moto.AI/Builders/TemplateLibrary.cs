// Moto.Core/AI/Builders/TemplateLibrary.cs
using System.Collections.Generic;
using Moto.Editor.AI.Builders;

namespace Moto.Core.AI.Builders
{
    /// <summary>
    /// Bibliothèque de templates de projets connus.
    /// Permet à MOTO AI de générer des projets complets SANS modèle externe :
    /// le "serpent Nokia 3310" est un template local, personnalisable.
    /// </summary>
    public class TemplateLibrary
    {
        /// <summary>
        /// Génère tous les fichiers d'un jeu Snake rétro (console, style Nokia 3310).
        /// </summary>
        public List<GeneratedFile> GetSnakeGameFiles(string projectName)
        {
            var files = new List<GeneratedFile>();

            files.Add(new GeneratedFile
            {
                RelativePath = $"{projectName}.csproj",
                Reason = "Projet console .NET 8.",
                Content = SnakeCsproj
            });

            files.Add(new GeneratedFile
            {
                RelativePath = "Program.cs",
                Reason = "Point d'entrée du jeu.",
                Content = SnakeProgram.Replace("PROJECTNAME", projectName)
            });

            files.Add(new GeneratedFile
            {
                RelativePath = "Game/SnakeGame.cs",
                Reason = "Boucle de jeu, logique du serpent, score, collisions.",
                Content = SnakeGameLogic.Replace("PROJECTNAME", projectName)
            });

            files.Add(new GeneratedFile
            {
                RelativePath = "README.md",
                Reason = "Documentation du jeu.",
                Content = $"# {projectName}\n\nJeu Snake rétro généré par MOTO AI, style Nokia 3310.\n\n" +
                          "## Lancer\n\n```\ndotnet run\n```\n\n" +
                          "## Contrôles\n\n- Flèches ou ZQSD : déplacer\n- ESC : quitter\n"
            });

            return files;
        }

        private const string SnakeCsproj = """
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>

</Project>
""";

        private const string SnakeProgram = """
using System;

namespace PROJECTNAME
{
    /// <summary>
    /// Point d'entrée du jeu Snake rétro.
    /// </summary>
    internal static class Program
    {
        private static void Main()
        {
            Console.Title = "PROJECTNAME - Nokia 3310 Style";
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var game = new SnakeGame(22, 14);
            game.Run();
        }
    }
}
""";

        private const string SnakeGameLogic = """
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PROJECTNAME
{
    public struct Point
    {
        public int X;
        public int Y;
    }

    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// Boucle de jeu du serpent, rendu monochrome façon écran Nokia 3310.
    /// </summary>
    public class SnakeGame
    {
        private readonly int _width;
        private readonly int _height;
        private readonly Random _rnd = new Random();

        private LinkedList<Point> _snake;
        private Point _food;
        private Direction _dir;
        private Direction _nextDir;
        private int _score;
        private int _speedMs;
        private bool _running;

        public SnakeGame(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public void Run()
        {
            do
            {
                Reset();
                Console.CursorVisible = false;

                while (_running)
                {
                    HandleInput();
                    Step();
                    Render();
                    Thread.Sleep(_speedMs);
                }

            } while (RenderGameOver());

            Console.CursorVisible = true;
        }

        private void Reset()
        {
            _snake = new LinkedList<Point>();

            var start = new Point { X = _width / 2, Y = _height / 2 };
            _snake.AddFirst(start);
            _snake.AddLast(new Point { X = start.X - 1, Y = start.Y });
            _snake.AddLast(new Point { X = start.X - 2, Y = start.Y });

            _dir = Direction.Right;
            _nextDir = Direction.Right;
            _score = 0;
            _speedMs = 160;
            _running = true;

            SpawnFood();
        }

        private void SpawnFood()
        {
            do
            {
                _food = new Point
                {
                    X = _rnd.Next(0, _width),
                    Y = _rnd.Next(0, _height)
                };
            }
            while (OnSnake(_food));
        }

        private bool OnSnake(Point p)
        {
            foreach (var s in _snake)
            {
                if (s.X == p.X && s.Y == p.Y)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleInput()
        {
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.Z:
                    case ConsoleKey.W:
                        SetDir(Direction.Up);
                        break;

                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        SetDir(Direction.Down);
                        break;

                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.Q:
                    case ConsoleKey.A:
                        SetDir(Direction.Left);
                        break;

                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        SetDir(Direction.Right);
                        break;

                    case ConsoleKey.Escape:
                        _running = false;
                        break;
                }
            }
        }

        private void SetDir(Direction d)
        {
            // Interdiction de faire un 180° direct.
            if ((d == Direction.Up && _dir == Direction.Down) ||
                (d == Direction.Down && _dir == Direction.Up) ||
                (d == Direction.Left && _dir == Direction.Right) ||
                (d == Direction.Right && _dir == Direction.Left))
            {
                return;
            }

            _nextDir = d;
        }

        private void Step()
        {
            _dir = _nextDir;

            var head = _snake.First.Value;
            var newHead = head;

            switch (_dir)
            {
                case Direction.Up: newHead.Y--; break;
                case Direction.Down: newHead.Y++; break;
                case Direction.Left: newHead.X--; break;
                case Direction.Right: newHead.X++; break;
            }

            // Murs + collision avec soi-même.
            if (newHead.X < 0 || newHead.Y < 0 ||
                newHead.X >= _width || newHead.Y >= _height ||
                OnSnake(newHead))
            {
                _running = false;
                return;
            }

            _snake.AddFirst(newHead);

            if (newHead.X == _food.X && newHead.Y == _food.Y)
            {
                _score += 10;

                if (_speedMs > 60)
                {
                    _speedMs -= 6;
                }

                SpawnFood();
            }
            else
            {
                _snake.RemoveLast();
            }
        }

        private void Render()
        {
            var sb = new StringBuilder();

            sb.Append("SCORE ").Append(_score)
              .Append("   VITESSE ").Append(160 - _speedMs).AppendLine();

            sb.Append('+').Append(new string('-', _width)).AppendLine("+");

            for (int y = 0; y < _height; y++)
            {
                sb.Append('|');

                for (int x = 0; x < _width; x++)
                {
                    var p = new Point { X = x, Y = y };

                    if (p.X == _food.X && p.Y == _food.Y)
                    {
                        sb.Append('o');
                    }
                    else if (OnSnake(p))
                    {
                        sb.Append('#');
                    }
                    else
                    {
                        sb.Append(' ');
                    }
                }

                sb.AppendLine("|");
            }

            sb.Append('+').Append(new string('-', _width)).AppendLine("+");
            sb.AppendLine("Fleches / ZQSD : deplacer - ESC : quitter");

            Console.SetCursorPosition(0, 0);
            Console.Write(sb.ToString());
        }

        private bool RenderGameOver()
        {
            Console.Clear();
            Console.WriteLine();
            Console.WriteLine("   GAME OVER");
            Console.WriteLine($"   SCORE FINAL : {_score}");
            Console.WriteLine();
            Console.WriteLine("   ENTREE : rejouer   -   ESC : quitter");

            while (true)
            {
                var key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.Enter)
                {
                    Console.Clear();
                    return true;
                }

                if (key == ConsoleKey.Escape)
                {
                    return false;
                }
            }
        }
    }
}
""";
    }
}
