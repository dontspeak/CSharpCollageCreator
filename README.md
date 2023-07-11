# CSharpCollageCreator
Picture collage creator that uses an evolutionary algorythm.

![Sample](Sample.png?raw=true)

# Introduction:
I googled a solution to generate *picture collages*, as I had the need to create one for a special case with 56 source images.
What I wanted was:
- no limitation of source image count
- no simple square orientation layout
- solution for images of arbitrary sizes
- solution for images of arbitrary aspect ratios
- auto-normalizing image sizes
- best possible utilization of the available space in output image
- no cropping of images
- no overlapping of images

I was surprised that actually there is no free-of-cost solution available that creates "true" image collages that fit my requirements. So I decided to implement one. Because I had deepened this topic at university, I used **evolutionary algorithms** to solve it.

# How do evolutionary algorithms (EA) work in general?
EA make use of the following pattern to solve a problem: A "solution" of a problem is created randomly. As this solution is created randomly, the way it "solves" the problem is not very good of course, as noone on earth put "brain" into creating this solution.

EA extend that simple approach and deals with "evolving" random solutions. In EA, a solution is called "*individual*".

There are **thausends of different** game types one can play EA, a common, very simple one, is the following (that I used):
- First, 100 individuals are generated randomly. We call this group of individuals "**parents**". For each parent, the "fitness" value is calculated. **The better the fitness, the better that individual "solves" the problem**.
- Now, we take out 2 parents randomly, "mix" them, and **create another individual as a "recombination" of the initial two**. This way, the recombined individual has properties of both the source individuals.
- This recombination is done 50 times. So we end up with additional 50 individuals, which we call "**children**". (Actually in my code 2 parents create 2 children, see comments).
- Some of the children are **slightly changed afterwards**, this is called "mutation"
- Out of the 70 individuals we have now (parents + children), we **choose the best 20 ones**. This is called "selection". These 20 will make it into the next generation and will be the 20 parents everything is starting with again. These new 20 individuals alltogether are a bit "better" then the initial 20, because they were selected as the 20 best out of 70. (Actually its a bit more complex as I used a "tournament selection", see code comments.)
- This procedure is repeated thausends of times. **Each generation (hopefully) slightly improves the individuals**.
- When we are satisfied with the best individual generated so far, we stop and use the solution.

# How have I used that EA approach to generate a picture collage?
Basically 99% of the program's runtime is calculating the fitness of individuals.

For all evolutionary approaches its always the **main** question: *How to define the fitness?* Most times, EA are optimization problems, were you want to achieve either a very large fitness-value or a very small one (maximizing-problem or minimizing-problem). 

In this case I choosed to implement it as a **minimizing-problem**. I.e. the smaller the fitnessvalue is, the "better" the individual is. The fitness calculation method works as follows:

An invidual is a certain **PERMUTATION** of the source images.
We have a predefined pixel *width* and *height* of the output image that we are aiming.
For each source image, the method searches *from top to down, left to right* **that** certain pixel position in the output image were the source image can be printed onto, **without** overlapping with another source image that was printed before. Because the source images are choosen randomly (based on the permutation an individual represents) some results are **better** (the images are "closer together" and make better use of the available space), some results are **worse** (there are bigger "holes" between images). The overall fitness value of an invidual is the pixel **height** that is needed to print out all source images. **The smaller this height is, the better the solution is.** (The width actually is fixed and the same for each solution within one evolutionary run.) When the currently best solution's height even is not larger than the predefined height of the output image we are aiming, we have found a final solution.

# Why is that "optimizing" at all?
Basically we're running random solutions, but these random solutions are optimized with an evolutionary algorythm during thausends of generations.

So when time goes by and more and more generations are improving, the currently best solution gets better and better.

**Its not science, its more heuristics. **

# Is it better then just trying out randomly?
Well, thats a good question. I quickly checked another solution that just tries out *randomly generated* permutations and let it run some minutes. Basically this approach also finds solutions. But they have more holes between the source images, at least for my set of input images. So I think actually the evolutionary approach is superior. I even added that random approach into the code, if  you want, you can try it out! I also think, **the more different the input images are** (i.e. source images differ in aspect ratio), the better the evolutionary approach is compared to the random one. If you find a solution that only works by chance, and is better than mine, just let me know!

Always remember: WWJD 

# Command line parameters

- `-i`
 - Path to folder with source images.
 - Default: *no*
- `-s`
 - Pixel width or height of each source image in final image collection. The larger this value is, the larger the output image will be in pixels. >= 16.
 - Default: *160*
- `-r`
 - Pixel aspect ratio width / height of output image. > 0.
 - Default: *1.414*
- `-f`
 - Downsampling factor for calculating the solution. The larger this value is, the faster a solution will be found, but the more the images will overlap. >= 1, <= 256.
 - Default: *10*
- `-t`
 - Amount of concurrent threads the software is using. Choose amount of your CPU cores for fastest calculation. >= 1, <= 32.
 - Default: *4*
- `-w`
 - Amount of seconds, the software tries to find a solution of a certain pixel width, until it re-starts the search with increased pixel width. The larger this value is, the better the final solution will be, but the longer the program runs. >= 3.
 - Default: *60*
- `-cr`
 - Color component 'R' of output image RGBA background color, >= 0, < 256.
 - Default: *0*
- `-cg`
 - Color component 'G' of output image RGBA background color, >= 0, < 256.
 - Default: *0*
- `-cb`
 - Color component 'B' of output image RGBA background color, >= 0, < 256.
 - Default: *0*
- `-ca`
 - Color component 'A' of output image RGBA background color, >= 0, < 256.
 - Default: *255*
 
 
 ## Sample
 
`.\CSharpCollageCreator.exe -i "C:\TEMP\images" -f 5 -w 120`
