

using System.ComponentModel;
using System.ComponentModel.Design;
using System.Data;
using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.InteropServices.ObjectiveC;
using System.Security.Cryptography;
using System.Threading;

namespace CSharpCollageCreator
{
    internal class Program
    {
        // CLI arguments:
        static String SOURCE;
        static int THUMBSIZE;
        static double RATIO;
        static int FACTOR;
        static int THREADS;
        static int POPULATIONLIFETIMESECONDS;
        static byte RGBA_R;
        static byte RGBA_G;
        static byte RGBA_B;
        static byte RGBA_A;

        // Definition of CLI arguments:
        static Dictionary<String, (String, String)> options = new()
        {
            {"i", ("N/A", "Path to folder with source images.")},
            {"s", ("160", "Pixel width or height of each source image in final image collection. The larger this value is, the larger the output image will be in pixels. >= 16")},
            {"r", ("1.414", "Pixel aspect ratio width / height of output image. > 0")},
            {"f", ("10", "Downsampling factor for calculating the solution. The larger this value is, the faster a solution will be found, but the more the images will overlap. >= 1, <= 256")},
            {"t", ("4", "Amount of concurrent threads the software is using. Choose amount of your CPU cores for fastest calculation. >= 1, <= 32")},
            {"w", ("60", "Amount of seconds, the software tries to find a solution of a certain pixel width, until it re-starts the search with increased pixel width. The larger this value is, the better the final solution will be, but the longer the program runs. >= 3")},
            {"cr", ("0", "color component 'R' of output image RGBA background color, >= 0, < 256")},
            {"cg", ("0", "color component 'G' of output image RGBA background color, >= 0, < 256")},
            {"cb", ("0", "color component 'B' of output image RGBA background color, >= 0, < 256")},
            {"ca", ("255", "color component 'A' of output image RGBA background color, >= 0, < 256")}
        };

        // process CLI arguments:
        static bool LoadArgument(string flag, string[] args, Func<String, bool> invalidate)
        {
            String arg = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Trim().ToLower() == "-" + flag.ToLower() && i < args.Length - 1)
                {
                    arg = args[i + 1];
                }
            }
            if (arg == null) arg = options[flag].Item1;

            if (invalidate(arg))
            {
                Console.WriteLine("Missing or invalid argument -" + flag + ": " + options[flag].Item2);
                return false;
            }
            return true;
        }


        // Configuration of evolutionary algorythm:
        static int EVO_N = 20; // amount of parents in one generation
        static int EVO_M = 50; // amount of children in one generation
        static double EVO_MUTATEPERCENT = 0.05; // this percentage of children is additionally mutated

        // image index:
        static Dictionary<int, (String file, Image image, int widht, int heigth, int calcWidth, int calcHeight)> images = new();


        // Calculates the "fitness" of an individual. For more information what is done here actually, read the project description.
        // Why dont you see image stuff in this method?
        //    Well, to get it running in finite time, I did the overlapping stuff with boolean arrays.But boy, its faaaaar away from beeing optimal!

        static void CalculateFitness(int[] ind, int width, int height, Image target)
        {
            bool[,] map = new bool[height, width ]; // hopefull thats faster than dealing with Bitmaps or else...

            int y = int.MinValue;
            int maxY = int.MinValue;
            int maxX = int.MinValue;
            for (int i = 0; i < ind.Length - 2; i++) // place each source image onto the output image, step by step
            {
                var individual = images[ind[i]]; // choose source image based on the individuals's permutation

                // just some vars to optimize speed (actually I think thats useless in .NET 7.0, the compiler already would optimize that...
                int iWidht = individual.calcWidth;
                int iHeight = individual.calcHeight;
                int ypRun;
                int xpRun;
                bool collision = false;

                // iterate pos from top to down, left to right. Find a pos, where the current source image can be printed onto, without 
                // overlapping with another source image that was printed before
                y = 0;
                int x = 0;
                do
                {
                    collision = false;
                    ypRun = y + iHeight;
                    xpRun = x + iWidht;
                    for (int yp = y; yp < ypRun; yp++)
                    {
                        for (int xp = x; xp < xpRun; xp++)
                        {
                            if (yp >= width || xp >= width)
                            {
                                int dd = 2;
                            }
                            if (map[yp, xp])
                            {
                                // pixel was already used before, must try next position
                                collision = true;
                                break;
                            }
                        }
                        if (collision) break;
                    }

                    if (!collision)
                    {
                        // position found, mark according pixels in output image as "used":
                        for (int yp = y; yp < ypRun; yp++)
                        {
                            for (int xp = x; xp < xpRun; xp++)
                            {
                                map[yp, xp] = true;
                            }
                        }

                        if (target != null)
                        {
                            // This prints the output image and is not done during normal fitness calculation (only one time when programm ends)
                            target.Mutate(o => o.DrawImage(images[ind[i]].image, new Point(x * FACTOR, y * FACTOR), 1f));
                        }

                        break;
                    }
                    x++; // step one pixel from left to right
                    if (x >= width - iWidht) // x must not reach the very right end (width), it can stop when the source image width would not any more fit to the right margin
                    {
                        x = 0;
                        y++; // step one pixel from top to down
                    }
                } while (collision);

                // preserve whats the best solution until now (to be able to calculate the fitness afterwards)
                if (maxY < ypRun) maxY = ypRun;
                if (maxX < xpRun) maxX = xpRun;

            }
            ind[ind.Length - 1] = maxY * width + maxX; // Finally store the fitness value into the invidual (last position in array)
        }





        static void Main(string[] args)
        {
            // Print out CLI help:
            if (args == null || args.Length == 0 || args.Contains("h") || args.Contains("help") || args.Contains("-h") || args.Contains("-help") || args.Contains("--h") || args.Contains("--help"))
            {
                Console.WriteLine("Start with following CLI arguments:");
                foreach (var option in options)
                {
                    Console.WriteLine("   -" + option.Key + ": " + option.Value.Item2);
                    Console.WriteLine("       default: " + option.Value.Item1);
                }
                return;
            }


            // Reading params, returning when invalid:
            Console.WriteLine("Reading params...");

            if (!LoadArgument("i", args, (arg) => ((SOURCE = arg) == null || !Directory.Exists(SOURCE)))) return;

            if (!LoadArgument("s", args, (arg) => (!int.TryParse(arg, out THUMBSIZE) || (THUMBSIZE < 16)))) return;

            if (!LoadArgument("r",  args, (arg) => (!double.TryParse(arg.Replace(",", "."), CultureInfo.InvariantCulture, out RATIO) || (RATIO <= 0)))) return;

            if (!LoadArgument("f",  args, (arg) => (!int.TryParse(arg, out FACTOR) || (FACTOR <= 0 || FACTOR > 256)))) return;

            if (!LoadArgument("t",  args, (arg) => (!int.TryParse(arg, out THREADS) || (THREADS <= 0 || THREADS > 32)))) return;

            if (!LoadArgument("w",  args, (arg) => (!int.TryParse(arg, out POPULATIONLIFETIMESECONDS) || (POPULATIONLIFETIMESECONDS < 3)))) return;

            if (!LoadArgument("cr", args, (arg) => (!byte.TryParse(arg, out RGBA_R)))) return;

            if (!LoadArgument("cg",  args, (arg) => (!byte.TryParse(arg, out RGBA_G)))) return;

            if (!LoadArgument("cb",  args, (arg) => (!byte.TryParse(arg, out RGBA_B)))) return;

            if (!LoadArgument("ca",  args, (arg) => (!byte.TryParse(arg, out RGBA_A)))) return;



            // Loading image files, ignoring files that cant be read:
            Console.WriteLine("Loading files...");
            int index = 0;
            foreach (var file in Directory.GetFiles(SOURCE))
            {
                try
                {
                    // Load image:
                    Image image = Image.Load(file);

                    // Align to THUMBSIZE, either width or height, depends on ratio of image:
                    int h, w;
                    if (image.Width >= image.Height)
                    {
                        h = (int)(image.Height * ((double)THUMBSIZE / image.Width));
                        w = THUMBSIZE;
                    }
                    else
                    {
                        h = THUMBSIZE;
                        w = (int)(image.Width * ((double)THUMBSIZE / image.Height));
                    }
                    image.Mutate(x => x.Resize(w, h));

                    // Adding to index:
                    images.Add(index, (file, image, w, h, w / FACTOR, h / FACTOR));
                    index++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("   ERROR when trying to load file '" + file + "': " + ex.Message);
                }
            }


            // Calculating start width. The result would have this width, if the software (theoretically) finds 
            // a 100% solution were EACH pixel of the result image is filled. As this never happens, this value is the startpoint
            // for the incremental search.
            long totalPixels = 0;
            int maxHeight = 0;
            foreach (var image in images)
            {
                totalPixels += (image.Value.calcWidth * image.Value.calcHeight);
                maxHeight += image.Value.calcHeight; // gets important if all images are aligned vertically one below the other...
            }
            int width = (int)Math.Ceiling(Math.Sqrt(RATIO * totalPixels));
            Console.WriteLine("   " + images.Count + " files successfully loaded");


            // Define the population size p, start evolutionary populations:
            int p = images.Count;
            int[] solution = null;


            // Countercheck with 100% randomly generated individuals
            if (false)
            {
                FindRandomSolution(width, p, maxHeight);
                return;
            }

            // Find solution with evolutionary approach:
            if (EVO_M % 2 != 0) EVO_M++; // m must be even because 2 parents recombine to 2 children

            while (true)
            {
                // Preparation:
                DateTime startTime = DateTime.Now;
                Console.WriteLine("Running population with width " + (width * FACTOR) + " pixels...");
                int height = (int)((double)width / RATIO); // we try to find a solution with a certain width, the height is derived from that width and aspect ratio
                Console.WriteLine("   target size: " + (width * FACTOR) + " x " + (height * FACTOR)); // If we find a solution that fits into this image size, were done.



                // Initialize population.
                // One individual is an int[] array with image order as permutation + fitness value, i.e. [0, 1, 2......p-1, <FITNESS>]
                // Sample individuals of size 3 and fitness 100:
                // [2, 0, 1, 100]
                // [1, 2, 0, 120]
                // [0, 1, 2, 90]
                Console.WriteLine("   initializing population...");
                int[][] parents = new int[EVO_N][];
                int[][] children = new int[EVO_M][];
                Random ran = new Random();

                // Generate n parents:
                for (int i = 0; i < EVO_N; i++)
                {
                    // Fill each parent with values 0, 1, 2, 3...p-1
                    int[] newParent = new int[p + 1];
                    for (int j = 0; j < p; j++)
                    {
                        newParent[j] = j;
                    }
                    // Exchange p^2 times one value with another. This creates a random permutation:
                    for (int j = 0; j < p * p; j++)
                    {
                        int p1 = ran.Next(p);
                        int p2 = ran.Next(p);

                        int temp = newParent[p1];
                        newParent[p1] = newParent[p2];
                        newParent[p2] = temp;
                    }
                    parents[i] = newParent;
                }

                // Calculate fitness of the initial generation multi-threated:
                for (int i = 0; i < EVO_N; i += THREADS)
                {
                    List<Task> tasks = new List<Task>();
                    for (int t = i; t < i + THREADS; t++)
                    {
                        if (t < EVO_N) // in case (p % THREADS != 0) this is false for the very last run
                        {
                            int[] ind = parents[t];
                            tasks.Add(Task.Factory.StartNew(() => CalculateFitness(ind, width, maxHeight, null)));
                        }
                    }
                    Task.WaitAll(tasks.ToArray()); // Execute fitness calculation in THREADS concurrent threads and wait until all is done
                }


                // Initialize children individuals with 0, 0, 0, ...
                for (int i = 0; i < EVO_M; i++)
                {
                    children[i] = new int[p + 1];
                }


                // Start evolution run
                Console.WriteLine("   running evolution...");
                int gen = 0;
                int minFitnessTotal;
                while (true)
                {
                    // Recombination:
                    // Two randomly choosen parents are recombined and create two children.
                    // This is a very primitive "one-point order crossover" algorythm.
                    // Sample: Two parents
                    // A, B, C, D
                    // D, C, B, A
                    // with crossover-point in the middle generate two children:
                    // A, B, D, C
                    // D, C, A, B
                    // (maybe a two-point crossover is better here....)
                    for (int i = 0; i < EVO_M / 2; i++)
                    {
                        int[] parent1 = parents[ran.Next(EVO_N)];
                        int[] parent2 = parents[ran.Next(EVO_N)];

                        int pos = ran.Next(p) + 1; // Crossover point is choosen randomly

                        int[] child1 = children[i];
                        int[] child2 = children[i + (EVO_M / 2)];

                        bool[] used1 = new bool[p];
                        bool[] used2 = new bool[p];

                        // until pos-1, child and first parent are identical
                        for (int j = 0; j < pos; j++)
                        {
                            child1[j] = parent1[j];
                            child2[j] = parent2[j];
                            used1[parent1[j]] = true;
                            used2[parent2[j]] = true;
                        }

                        // for the second part, the child contains the remaining values IN ORDER OF THE SECOND parent
                        int child1Pos = pos;
                        int child2Pos = pos;
                        for (int j = 0; j < p; j++)
                        {
                            if (!used1[parent2[j]])
                            {
                                child1[child1Pos] = parent2[j];
                                child1Pos++;
                            }
                            if (!used2[parent1[j]])
                            {
                                child2[child2Pos] = parent1[j];
                                child2Pos++;
                            }
                        }
                    }

                    // Mutation:
                    // With a certain probability, mutate a child after it was born.
                    // Very primitive, just two positions are exchanged
                    for (int i = 0; i < EVO_M * EVO_MUTATEPERCENT; i++)
                    {
                        int[] indToMutate = children[ran.Next(EVO_M)];
                        int pos0 = ran.Next(p);
                        int pos1 = ran.Next(p);
                        int temp = indToMutate[pos0];
                        indToMutate[pos0] = indToMutate[pos1];
                        indToMutate[pos1] = temp;
                    }

                    // Calculate fitness of all generated children multi-threated (see above):
                    for (int i = 0; i < EVO_M; i += THREADS)
                    {
                        List<Task> tasks = new List<Task>();
                        for (int t = i; t < i + THREADS; t++)
                        {
                            if (t < EVO_M)
                            {
                                int[] ind = children[t];
                                tasks.Add(Task.Factory.StartNew(() => CalculateFitness(ind, width, maxHeight, null)));
                            }
                        }
                        Task.WaitAll(tasks.ToArray());
                    }


                    // Selection:
                    // We have n+m individuals now (parents + children), but for next generation we need
                    // to get back to the population size n. Thats why the best individuals
                    // (based on their fitness value) will "survive" and move into the next generation.
                    // A simple "tournament selection" with size 3 is used. I.e. 3 individuals are
                    // choosen randomly, the one best of these 3 will win and make it into the next generation:
                    minFitnessTotal = int.MaxValue; // Store fitness of currently best individual (the lower the value the better the fitness)
                    int[][] newPopulation = new int[EVO_N][];
                    for (int i = 0; i < EVO_N; i++)
                    {
                        // Select 3 individuals randomly:
                        int ind1Index = ran.Next(EVO_N + EVO_M);
                        int ind2Index = ran.Next(EVO_N + EVO_M);
                        int ind3Index = ran.Next(EVO_N + EVO_M);
                        int[] ind1 = ind1Index < EVO_N ? parents[ind1Index] : children[ind1Index - EVO_N];
                        int[] ind2 = ind2Index < EVO_N ? parents[ind2Index] : children[ind2Index - EVO_N];
                        int[] ind3 = ind3Index < EVO_N ? parents[ind3Index] : children[ind3Index - EVO_N];

                        // Calculate the best of these 3:
                        int[] minInd = ind1;
                        if (ind2[p] < minInd[p])
                        {
                            minInd = ind2;
                        }
                        if (ind3[p] < minInd[p])
                        {
                            minInd = ind3;
                        }
                        // Put the best into the new generation:
                        newPopulation[i] = (int[])minInd.Clone();

                        // Have we found a newer, better individual?
                        if (minInd[p] < minFitnessTotal)
                        {
                            minFitnessTotal = minInd[p];

                            // Is this individual even a solution for our problem?
                            if ((minFitnessTotal / width) <= height)
                            {
                                solution = newPopulation[i];
                            }
                        }
                    }
                    if (solution != null) break; // We found a solution, exit programm!

                    // The parents of the new generation are the choosen ones:
                    parents = newPopulation;
                    gen++;

                    // Break search with current width (as obviously we havent found a solution in the given timeframe)
                    // Continue trying with a larger width in next run, so exit evolution now.
                    if ((DateTime.Now - startTime).TotalSeconds >= POPULATIONLIFETIMESECONDS)
                    {
                        Console.WriteLine("   not found within " + gen + " populations, minimum height was " + ((minFitnessTotal / width) * FACTOR) + " pixels");
                        break;
                    }
                }

                // When a solution was found, we have to create the output image and exit:
                if (solution != null)
                {
                    Console.WriteLine("   solution found with height " + ((minFitnessTotal / width) * FACTOR) + " pixels");
                    Console.WriteLine("   printing...");
                    StoreOutputImage(solution, width, height, maxHeight);
                    break;
                }
                width++;
            }
        }

        // Once a solution was found, create the png file:
        private static void StoreOutputImage(int[] solution, int width, int height, int maxHeigth)
        {
            var outputImage = new Image<Rgba32>(width * FACTOR, height * FACTOR, new Rgba32(RGBA_R, RGBA_G, RGBA_B, RGBA_A));
            CalculateFitness(solution, width, maxHeigth, outputImage);
            outputImage.Save(DateTime.Now.ToString("yyMMddmmHHss") + ".png");



        }





        // This is just a countercheck with 100% randomly generated individuals.
        private static void FindRandomSolution(int width, int p, int maxHeigth)
        {

            Random ran = new Random();
            int[] solution;
            int height;
            do
            {
                DateTime start = DateTime.Now;
                height = (int)((double)width / RATIO);
                Console.WriteLine("Searching random solution with " + (width * FACTOR) + " x " + (height * FACTOR) + " pixels...");

                do
                {
                    solution = new int[p + 1];
                    solution[p] = int.MaxValue;

                    int[] newParent = new int[p + 1];
                    for (int j = 0; j < p; j++)
                    {
                        newParent[j] = j;
                    }
                    for (int j = 0; j < p * p; j++)
                    {
                        int p1 = ran.Next(p);
                        int p2 = ran.Next(p);

                        int temp = newParent[p1];
                        newParent[p1] = newParent[p2];
                        newParent[p2] = temp;
                    }

                    CalculateFitness(newParent, width, maxHeigth, null);
                    if (solution[p] > newParent[p])
                    {
                        solution = newParent;
                    }

                } while ((DateTime.Now - start).TotalSeconds < POPULATIONLIFETIMESECONDS);

                if ((solution[p] / width) <= height)
                {
                    break; // solution found that fits into widht x height
                }
                width++;
            } while (true);

            Console.WriteLine("   random solution found, printing image...");
            StoreOutputImage(solution, width, height, maxHeigth);
        }
    }
}