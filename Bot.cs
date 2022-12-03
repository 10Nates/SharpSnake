using System;
using System.Collections.Generic;
using System.Linq;

namespace Snek
{
    internal class Bot
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