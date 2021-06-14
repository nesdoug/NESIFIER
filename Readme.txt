NESIFIER
(c) Doug Fraker, 2021


This is an image processing tool for nesdev (NES game development).

You load an image into it (.gif, .bmp, .jpg, .png), and you can
convert an image into a NES format graphics file. You can save
the image in NES graphics format 2bpp CHR file and also output
the palette and a nametable.

Optionally, you can import an image from the clipboard.

The image should be 256x240 or smaller, before importing.
(Note, 128x128 is the maximum 256 BG tiles, but duplicate
tiles will be removed)

Using the resize options is probably going to be different than
you expect. Those are maximum sizes for each dimension, and
the resize algorithm always maintains the size ratio. So, if
your source image is 256x256 and you set the resize boxes to
100x240, it will resize to 100x100. Likewise, if you set the
boxes to 256x80, it will resize to 80x80.

You will have to RELOAD, or REPASTE from clipboard, if you
change the size values.

Best results will come from resizing an image in another app,
then increasing the canvas size to 256x240, centering as needed,
then select all, copy, and paste from clipboard (press V) in 
Nesifier.

Once the image is loaded, there are 4 ways to select a palette.
-click on the NES Palette colors (then click on a color box)
-click on the image to select colors (then click on a color box)
-press the Auto Generate Palette button
-load a palette file (12 byte RGB ..or.. a 4 or 16 byte NES)

Optionally, you could press the Grayscale button to convert the
original image to grayscale before processing. That button also
fills the palette with black, white, and 2 grays.


Then, select a dither level (0 is off, 10 is normal, 11-12 is a 
little extra), and a method. I prefer a dither factor of 6-8.


Then, press the "convert" button to convert the image to 
4 colors of NES palette. Convert applies the dither selection.

The "Revert" button to see the original image again. But, in
most cases you won't need to hit this button.

Auto Generate Palette button is not perfect. You will probably
need to manually change a color or two. Also, if there is a large
number of colors for it to consider, it could take a few seconds
to get the result. 

If a color doesn't use up at least (roughly) 10% of the area
of the picture, there is a high probability that it won't be
selected. For example, if the image is 90% shades of gray
and 10% red, it might decide to remove all reds. In that case
you will have to manually select a red.

It will suggest a 5th color, and put it in the Selected Color box.

Finally, save the image and/or the CHR / Nametable / Palette. 

This has been changed. You will see 2 different ways to save
CHR. 
-Save RAW will save without removing duplicates.
-Save Final will save after removing duplicates. You want this one.
You want a CHR file with 256 tiles or less.

Save Final is what corresponds to the save nametable option.
The nametable file includes the attribute table, but all zeroes.

Save Final CHR and Save Nametable can also use the compression
system designed by me (Doug Fraker), called dz4. See the DZ4
folder for the 6502 asm code to decompress it. DZ4 is very
good for compressing nametables, but mediocre for CHR files.

You can also save the palette, in RGB or NES format. You can also
copy the NES palette to the clipboard as text (for coding in
assembly). The "save NES 16 byte" option is so it works with
NES Screen Tool.


Notes:
Left clicking on a color box will copy the selected color there.

Right clicking on a color box will copy that box to the
selected color box.

You might get better results just clicking grayscale, converting
the image, and then move the files over to NES screen tool,
before giving it a color palette.


Credit: 
the palette was taken from FBX/FirebrandX Smooth NES palette.

 
