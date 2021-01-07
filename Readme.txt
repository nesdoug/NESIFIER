NESIFIER
(c) Doug Fraker, 2021

This is an image processing tool for nesdev (NES game development).

You load an image into it (.gif, .bmp, .jpg, .png), and you can
convert an image into a NES palette image. You can even save
the image in NES graphics format 2bpp CHR file and also output
the palette.

Optionally, you can import an image from the clipboard.

The image should be 256x256 or smaller. Any larger will be cropped.
You may need to resize in another app before importing.
By the way, 256x240 is the size of NES output. However, you
may prefer to use 128x128, which is the same as 256 tiles.

Once the image is loaded, there are 4 ways to select a palette.
-click on the NES Palette colors (then click on a color box)
-click on the image to select colors (then click on a color box)
-press the Auto Generate Palette button
-load a palette file (12 byte RGB ..or.. 4 byte NES)

Then, select a dither level (0 is off, 10 is normal, 11-12 is a 
little extra) and method (Floyd Steinburd error diffusion or
Bayer 8x8 positioned dithering by brightness). I prefer a dither
factor of 7 or 8.

Optionally, you could press the Grayscale button to convert the
original image to grayscale before processing.

Then, press the "convert" button to convert the image to 
4 colors of NES palette.

You could press the "convert" button multiple times with different
palette choices. It will always convert from the original, even
if you never "revert" it. The "revert" button just puts the
original picture visually in the box, but it doesn't do anything
functional.

Auto Generate Palette button is not perfect. You will probably
need to manually change a color or two. Also, if there is a large
number of colors for it to consider, it could take a few seconds
to get the result. 

If a color doesn't use up at least (roughly) 10% of the area
of the picture, there is a high probablility that it won't be
selected. For example, if the image is 90% shades of gray
and 10% red, it might decide to remove all reds.

Sometimes it will choose two of the same color. This is an issue
that will be improved in future versions.

Finally, save the image and/or the CHR file. You can optionally
change the index of the CHR output by clicking on the 4 buttons
on the bottom right (0,1,2,3). The CHR file can be opened with
a CHR editor, such as YY-CHR. The CHR file may be too big for
your needs, and you may need to reorganize the tiles. That will
have to be done manually... perhaps with 2 YY-CHR windows open
at the same time. You will have an easier time if the original
image was 128x128 pixels. Note that NES Screen Tool has an
option to find and remove duplicate tiles. 

You can also save the palette, in RGB or NES format. You can also
copy the NES palette to the clipboard as text (for coding in
assembly).


Credit: the palette was taken from FBX/FirebrandX Smooth NES palette.

