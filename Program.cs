using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;

namespace Snek // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static void Main() //(string[] args)
        {
            Console.WriteLine("Starting game...");
            bool continueGame = true;
            while (continueGame)
            {
                Game _ = new(); // initialize

                Console.Clear();
                Console.WriteLine("Would you like to play again? (Y/N): ");
                ConsoleKey resp = Console.ReadKey().Key;

                if (resp != ConsoleKey.Y) continueGame = false;
                else Console.WriteLine("Creating new game...");
            }
        }
    }

    class Field
    {
        public const int size = Game.size + 2;

        private Random rnd = new();
        private int[] foodPos = new int[2];
        private List<Snake.Position> pastSnakePos = new();
        private short[,] tiles = new short[size, size];

        /*
         Tilemap
            0 : void
            1 : empty
            2 : food
            3 : uncollidable snake
            -1 : snake
            -2 : barrier
            -3 : out of bounds
            -4 : snake head
         */

        public Field()
        {
            GenerateNewField();
            NewFoodPosition(); // requires a field to function
            GenerateNewField(); // still need the food on the field
        }

        public struct FieldItems
        {
            public short tile;
            public int x;
            public int y;
        }

        public void NewFoodPosition()
        {
            int newx;
            int newy;
            bool validPos = false;
            while (!validPos)
            {
                newx = rnd.Next(Game.size);
                newy = rnd.Next(Game.size);

                if (GetTile(newx, newy) == 1)
                {
                    foodPos[0] = newx;
                    foodPos[1] = newy;
                    validPos = true;
                }
            }
        }

        public List<Snake.Position> UpdatePastSnakePos(uint ssize, Snake.Position newPos)
        {
            pastSnakePos.Add(newPos);
            if (pastSnakePos.Count > ssize)
            {
                pastSnakePos.RemoveAt(0);
            }

            return pastSnakePos;
        }

        private FieldItems[] GenerateNewFieldItems()
        {
            List<FieldItems> newFieldItems = new(); // needs to expand

            newFieldItems.Add(new FieldItems()
            {
                tile = 2, // food
                x = foodPos[0],
                y = foodPos[1]
            });

            // pastSnakePos updated BEFORE this method so new position only has to be managed once
            int i = 0;
            foreach (Snake.Position pos in pastSnakePos)
            {
                short tileValue = -1;
                if (i == 0) tileValue = 3;
                // The very back of the snake is not collidable
                // as collisions are calculated before the snake moves
                if (i == pastSnakePos.Count - 1) tileValue = -4;
                // Head of snake is different

                newFieldItems.Add(new FieldItems()
                {
                    tile = tileValue, // snake
                    x = pos.x,
                    y = pos.y
                });

                i++;
            }

            return newFieldItems.ToArray();

        }

        public void GenerateNewField()
        {
            FieldItems[] items = GenerateNewFieldItems();

            short[,] newtiles = new short[size, size]; // buffer

            for (int i = 0; i < size; i++) // empty field
            {
                for (int j = 0; j < size; j++)
                {
                    if (i == 0 || j == 0 || i == size - 1 || j == size - 1)
                    {
                        newtiles[i, j] = -2; // barrier
                    }
                    else
                    {
                        newtiles[i, j] = 1; // empty
                    }
                }
            }

            foreach (FieldItems item in items) // add items to field
            {

                newtiles[item.x + 1, item.y + 1] = item.tile;
            }

            tiles = newtiles;
        }

        public short[,] GetTiles()
        {
            return tiles;
        }

        public short GetTile(int x, int y)
        {
            if (x < Game.size && y < Game.size)
            {
                return tiles[x + 1, y + 1]; // don't include barrier
            }
            else
            {
                return -3;
            }
        }

        public int[] GetFoodPos()
        {
            return foodPos;
        }
    }

    class Snake
    {
        public struct Position
        {
            public int x;
            public int y;
        }

        public byte direction = 0; // 0 -> up / 1 -> left / 2 -> down 3 -> right
        public Position currentPos = new() { x = Game.midsize, y = Game.midsize };

        public uint size = 1;

        private Field activeField;

        public Snake(Field field)
        {
            activeField = field;
            activeField.UpdatePastSnakePos(size, currentPos);
        }

        public void MoveSnake(Game activeGame)
        {
            switch (direction)
            {
                case 0:
                    currentPos.y += 1;
                    break;
                case 1:
                    currentPos.x += 1;
                    break;
                case 2:
                    currentPos.y -= 1;
                    break;
                case 3:
                    currentPos.x -= 1;
                    break;
                default:
                    throw new Exception("Invalid direction");
            }
            short nextTile = activeField.GetTile(currentPos.x, currentPos.y);
            if (nextTile < 1)
            {
                activeGame.KillSnake();
                return;
            }
            switch (nextTile)
            {
                case 3: // uncollidable snake
                    goto case 1;
                case 2: //food
                    size++;
                    activeField.NewFoodPosition();
                    goto case 1;
                case 1:
                    activeField.UpdatePastSnakePos(size, currentPos);
                    activeField.GenerateNewField();
                    break;
                default:
                    throw new Exception("Tile does not exist");
            }
        }
    }

    class Game
    {
        public const int size = 12;
        public const int targetStartingFPS = 3;
        public const int midsize = size / 2;
        public const bool adjustSpeed = true;
        public bool dead = false;
        public bool keepAwake = true;

        // for testing
        public bool debug = false;
        public bool pathfind = false;
        public bool timewarp = false;

        private Field activeField;
        private Snake activeSnake;
        private Bot activeBot;

        private List<ConsoleKey> inputQueue = new();
        // private short[,] lastTiles = new short[Field.size, Field.size]; // Intended to improve IO performance but didn't change & caused a lot of visual issues

        private DateTime TimeStarted;
        private DateTime TimeDead;

        private System.Timers.Timer activeGameTicker;
        private List<double> averageTickRate;

        public Game()
        {
            activeField = new Field();
            activeSnake = new Snake(activeField);
            TimeStarted = DateTime.UtcNow;
            activeBot = new Bot(activeField, activeSnake, this);
            averageTickRate = new List<double> { };


            Console.Clear();

            // https://www.educative.io/answers/how-to-create-a-timer-in-c-sharp
            System.Timers.Timer gameTicker = new(1000 / targetStartingFPS);
            gameTicker.Elapsed += GameLoop;
            gameTicker.AutoReset = true;
            gameTicker.Start();
            activeGameTicker = gameTicker;

            Thread inputThread = new(new ThreadStart(() =>
            {
                InputCheck();
            }));
            inputThread.Start();

            while (keepAwake)
            {
                Thread.Sleep(100); // maintain main thread activity to allow for new game creation
            }
        }

        private void GameLoop(object o, ElapsedEventArgs e)
        {
            DateTime fpsCheck = DateTime.UtcNow;
            if (dead)
            {
                activeGameTicker.Stop(); // stop frame creation
                Console.Clear();

                // deadswitch
                Console.SetCursorPosition(0, 0);
                Console.WriteLine("Score: " + activeSnake.size);
                Console.WriteLine("Time played: " + (TimeDead - TimeStarted).TotalSeconds.ToString() + " seconds");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                keepAwake = false; // hands back to main thread
                return;

                //inputQueue = new List<ConsoleKey> { ConsoleKey.C }; // simulates exit key

                //activeGameTicker.Start(); // allow exit process

                // At this point, there is no more ticker and the input thread has stopped runnig.
                // This means that there is nothing else on the Game thread, so the game closes automatically.
            }


            ConsoleKey lastInput;
            if (inputQueue.Count > 0)
            {
                lastInput = inputQueue.First(); // pressing multiple keys in the same frame will rollover
                inputQueue.RemoveAt(0);
            }
            else
            {
                lastInput = ConsoleKey.Execute; // filler
            }

            switch (lastInput) // Input needs to be on main loop
            {
                case ConsoleKey.W:
                    if (activeSnake.direction != 2) // prevent turning 180 degrees
                    {
                        activeSnake.direction = 0;
                    }
                    break;
                case ConsoleKey.D:
                    if (activeSnake.direction != 3)
                    {
                        activeSnake.direction = 1;
                    }
                    break;
                case ConsoleKey.S:
                    if (activeSnake.direction != 0)
                    {
                        activeSnake.direction = 2;
                    }
                    break;
                case ConsoleKey.A:
                    if (activeSnake.direction != 1)
                    {
                        activeSnake.direction = 3;
                    }
                    break;
                case ConsoleKey.Tab:
                    Console.Clear();
                    if (debug) debug = false;
                    else debug = true;
                    break;
                case ConsoleKey.P:
                    Console.Clear();
                    if (pathfind) pathfind = false;
                    else pathfind = true;
                    break;
                case ConsoleKey.L:
                    Console.Clear();
                    if (timewarp) timewarp = false;
                    else timewarp = true;
                    break;
                case ConsoleKey.K:
                    //debug killswitch
                    KillSnake();
                    break;
                case ConsoleKey.Escape:
                case ConsoleKey.C:
                    activeGameTicker.Stop();
                    Console.Clear();
                    Console.WriteLine("Game closed.");
                    Environment.Exit(0);
                    break;
            }

            // actual game calculations
            activeSnake.MoveSnake(this);
            activeGameTicker.Interval = 1000 / (targetStartingFPS + (0.5 * Math.Sqrt(activeSnake.size - 1))); // gradually speeds up as game progresses

            // display the field to the user

            DateTime drawTimer = DateTime.UtcNow;
            short[,] tiles = activeField.GetTiles();
            ConsoleColor currentFC = ConsoleColor.White; // reduce IO clutter since it's by far the biggest perfomance hitch
            for (int i = 0; i < Field.size; i++)
            {
                for (int j = 0; j < Field.size; j++)
                {
                    short tile = tiles[i, j];
                    Console.SetCursorPosition(i * 2 + 2, Field.size - j); // *2 spaces it out better
                    switch (tile)
                    {
                        case 1: // empty
                            //Console.ResetColor(); // Color doesn't need to be changed since it's an empty tile
                            Console.Write(' ');
                            break;
                        case 2: // food
                            currentFC = ConsoleColor.Red; // Since this is not drawn repeatedly it would only hurt to have an if statement
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write('@');
                            break;
                        case 3:
                            goto case -1;
                        case -1: // snake
                            if (currentFC != ConsoleColor.DarkGreen)
                            {
                                currentFC = ConsoleColor.DarkGreen;
                                Console.ForegroundColor = ConsoleColor.DarkGreen;
                            }
                            Console.Write('#');
                            break;
                        case -4: // snake head
                            currentFC = ConsoleColor.Green; // Since this is not drawn repeatedly it would only hurt to have an if statement
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write('#');
                            break;
                        case -2: // barrier
                            if (currentFC != ConsoleColor.Gray)
                            {
                                currentFC = ConsoleColor.Gray;
                                Console.ForegroundColor = ConsoleColor.Gray;
                            }
                            Console.Write('*');
                            break;
                        case -3: // out of bounds
                            currentFC = ConsoleColor.Yellow; // This shouldn't appear anyways
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write('*');
                            break;
                        default: // void and whatever else gets thrown in by a cosmic ray
                            throw new Exception("This should not happen - tile does not exist");
                    }
                    // Console.ResetColor(); // This takes up a comical amount of processing power
                }
            }

            Console.ResetColor();

            //Right of play area
            Console.SetCursorPosition(Field.size * 2 + 3, 2);
            Console.Write("Size: " + activeSnake.size);
            Console.SetCursorPosition(Field.size * 2 + 3, 3);
            TimeSpan currentTime = DateTime.UtcNow - TimeStarted;
            Console.Write("Time (s): " + (int)(currentTime.TotalSeconds * 1000) / 1000.0);

            //Average Tickrate handling
            averageTickRate.Add((DateTime.UtcNow - fpsCheck).TotalMilliseconds);
            if (averageTickRate.Count > 300) { averageTickRate.RemoveAt(0); } // Prevent memory leak

            if (pathfind) AutomaticPlayStep(); // AI - very heavy workload

            // Displayed if you press tab
            if (debug)
            {
                Console.SetCursorPosition(Field.size * 2 + 3, 5);
                Console.Write("Snake Pos: ({0}, {1})", activeSnake.currentPos.x, activeSnake.currentPos.y);
                Console.SetCursorPosition(Field.size * 2 + 3, 6);
                int[] currentFoodPos = activeField.GetFoodPos();
                Console.Write("Apple Pos: ({0}, {1})", currentFoodPos[0], currentFoodPos[1]);
                Console.SetCursorPosition(Field.size * 2 + 3, 7);
                Console.Write("Target FT (ms): " + (int)(activeGameTicker.Interval) + " ");
                Console.SetCursorPosition(Field.size * 2 + 3, 8);
                Console.Write("Avg FT (ms): " + (int)(averageTickRate.Average() * 1000) / 1000.0 + " ");
                Console.SetCursorPosition(Field.size * 2 + 3, 9);
                Console.Write("Real FT (ms): " + (int)((DateTime.UtcNow - fpsCheck).TotalMilliseconds * 1000) / 1000.0 + " ");
            }

            // For input
            Console.SetCursorPosition(0, Field.size + 3);

            if (timewarp) activeGameTicker.Interval = 0.000001; // Tends to cause weird activity if not run last
        }

        private void InputCheck()
        {
            while (!dead)
            {
                ConsoleKey key = Console.ReadKey(true).Key; // true hides key from console
                if (inputQueue.Count == 0 || inputQueue.First() != key) // if there is nothing -> OR if it is not equal
                {
                    inputQueue.Add(key);
                }
            }
        }

        public void KillSnake()
        {
            TimeDead = DateTime.UtcNow;
            dead = true;
        }

        // For game simulation, currently works horribly
        public void AutomaticPlayStep()
        {
            inputQueue = activeBot.ModifyQueue(inputQueue);
        }
    }

    class Bot
    {
        private Field activeField;
        private Snake activeSnake;

        public Bot(Field field, Snake snake, Game game)
        {
            activeField = field;
            activeSnake = snake;
        }

        public List<ConsoleKey> ModifyQueue(List<ConsoleKey> inputQueue)
        {
            if (inputQueue.Count > 0)
            {
                return inputQueue;
            }
            else
            {
                return RenewQueue(inputQueue);
            }
        }

        private List<ConsoleKey> RenewQueue(List<ConsoleKey> inputQueue)
        {
            Snake.Position snakePos = activeSnake.currentPos;
            int[] foodPos = activeField.GetFoodPos();
            short[,] tiles = activeField.GetTiles();
            int arraySideSize = tiles.GetLength(0);
            int[,] pathMap = new int[arraySideSize, arraySideSize];

            // Fill map with barriers
            for (int i = 0; i < arraySideSize; i++)
            {
                for (int j = 0; j < arraySideSize; j++)
                {
                    if (i == 0 || j == 0 || i == arraySideSize - 1 || j == arraySideSize - 1 || tiles[i, j] < 1) // tiles have the borders & snake
                    {
                        pathMap[i, j] = -2; // All obstructions identifier
                    }
                    else
                    {
                        pathMap[i, j] = -1;
                    }
                }
            }

            pathMap[snakePos.x + 1, snakePos.y + 1] = 0; // distance to head


            // Create distance map
            bool foodFound = false;

            while (!foodFound)
            {
                if (pathMap[foodPos[0] + 1, foodPos[1] + 1] != -1)
                {
                    foodFound = true; // No need to continue calculations
                    break;
                }

                bool diff = false;

                for (int i = 1; i < arraySideSize; i++)
                {
                    for (int j = 1; j < arraySideSize; j++)
                    {
                        if (pathMap[i, j] != -1) continue; // Border or snake

                        int[] candidateHosts = { pathMap[i, j], pathMap[i + 1, j], pathMap[i - 1, j ],
                                                 pathMap[i, j + 1], pathMap[i, j - 1] }; // adjacent tiles
                        int host = -2;
                        for (int k = 0; k < candidateHosts.Length; k++)
                        {
                            if ((host == -2 && candidateHosts[k] != -1 && candidateHosts[k] != -2) || (candidateHosts[k] < host && candidateHosts[k] != -1 && candidateHosts[k] != -2)) // find smallest != -1
                            {
                                host = candidateHosts[k];
                            }
                        }

                        if (host != -2)
                        {
                            pathMap[i, j] = host + 1; // 1 away
                            diff = true;
                        }
                    }
                }

                if (!diff) break; // prevent haulting when no path is available - will still run into a wall

            }

            /*for (int i = 0; i < arraySideSize; i++)
            {
                for (int j = 0; j < arraySideSize; j++)
                {
                    Console.SetCursorPosition(i * 4 + 50, j); // debug
                    Console.Write(pathMap[i, j] + "   ");
                }
            }*/

            if (!foodFound) // No path to food -> collision avoidance
            {
                ConsoleKey direction;
                int[] curSPos = new int[2] { snakePos.x + 1, snakePos.y + 1 }; // current Position as snake

                int[] candidateHosts = { pathMap[curSPos[0] + 1, curSPos[1]], pathMap[curSPos[0] - 1, curSPos[1]],
                                         pathMap[curSPos[0], curSPos[1] + 1], pathMap[curSPos[0], curSPos[1] - 1] }; // adjacent tiles
                int hostID = -1;
                for (int k = 0; k < candidateHosts.Length; k++)
                {
                    if (candidateHosts[k] != -2)
                    {
                        hostID = k;
                        break;
                    }
                }

                switch (hostID)
                {
                    case 0:
                        direction = ConsoleKey.D; // Directions are NOT reversed because it's from the perspective of the snake
                        break;
                    case 1:
                        direction = ConsoleKey.A;
                        break;
                    case 2:
                        direction = ConsoleKey.W;
                        break;
                    case 3:
                        direction = ConsoleKey.S;
                        break;
                    default:
                        //throw new Exception("Invalid HostID");
                        direction = ConsoleKey.Execute; // Filler, do nothing
                        break;
                }

                return new List<ConsoleKey>() { direction };
            }


            // Backpropogate against distance map
            List<ConsoleKey> directions = new() { };
            int[] curPos = new int[2] { foodPos[0] + 1, foodPos[1] + 1 }; // current Position as food

            for (int i = 0; i < pathMap[foodPos[0] + 1, foodPos[1] + 1]; i++) // for distance to head
            {
                if (pathMap[curPos[0], curPos[1]] == 0)
                {
                    break;
                }

                int[] candidateHosts = { pathMap[curPos[0] + 1, curPos[1]], pathMap[curPos[0] - 1, curPos[1]],
                                         pathMap[curPos[0], curPos[1] + 1], pathMap[curPos[0], curPos[1] - 1] }; // adjacent tiles
                int hostSize = arraySideSize * arraySideSize + 10; // impossibly large
                int hostID = -1;
                for (int k = 0; k < candidateHosts.Length; k++)
                {
                    if (candidateHosts[k] < hostSize && candidateHosts[k] != -1 && candidateHosts[k] != -2)
                    {
                        hostSize = candidateHosts[k];
                        hostID = k;
                    }
                }

                switch (hostID)
                {
                    case 0:
                        curPos[0] += 1;
                        directions.Insert(0, ConsoleKey.A); // directions are reversed because it's coming from the food
                        break;
                    case 1:
                        curPos[0] -= 1;
                        directions.Insert(0, ConsoleKey.D);
                        break;
                    case 2:
                        curPos[1] += 1;
                        directions.Insert(0, ConsoleKey.S);
                        break;
                    case 3:
                        curPos[1] -= 1;
                        directions.Insert(0, ConsoleKey.W);
                        break;
                    default:
                        throw new Exception("Invalid HostID");
                }
            }

            /*Console.SetCursorPosition(0, 20);
            Console.Write("                                          ");
            for (int i = 0; i < directions.Count; i++)
            {
                Console.SetCursorPosition(i + 50, 20); // debug
                Console.Write(directions[i].ToString());
            }*/

            return new List<ConsoleKey>() { directions.First() };

        }
    }
}