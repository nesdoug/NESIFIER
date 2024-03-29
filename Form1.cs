﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NESIFIER
{
    public partial class Form1 : Form
    {
        // Programmer thoughts
        // a lot of this code is dulplicate of other code and
        // should be combined into common functions

        // other things should be refactored... particularly
        // the conversion to dithering, which is slow and convoluted

        // todo -- lossy ?
        // not sure how to do that


        public const int FLOYD_STEIN = 0;
        public const int BAYER8 = 1;
        public const int SCANLINE = 2;
        public const int BAYER2 = 3;
        public const int TRIANGLE = 4;

        public readonly int[,] BAYER_MATRIX =
        {
            { 0,48,12,60,3,51,15,63 },
            { 32,16,44,28,35,19,47,31 },
            { 8,56,4,52,11,59,7,55 },
            { 40,24,36,20,43,27,39,23 },
            { 2,50,14,62,1,49,13,61 },
            { 34,18,46,30,33,17,45,29 },
            { 10,58,6,54,9,57,5,53 },
            { 42,26,38,22,41,25,37,21 }
        }; // 1/64 times this

        public readonly int[,] BAD_MATRIX =
        {
            { -25, 15 },
            { 35, -5 }
        }; // -14, 14, 14, -14

        public readonly int[,] TRI_MATRIX =
        {
            { -20, 20, -20, -20 },
            { 20, 40, 20, -40 },
            { -20, -20, -20, 20 },
            { 20, -40, 20, 40 }
        };

        public const int BAYER_MULT = 64;
        public static int dither_factor = 0;
        public static int dither_adjust = 0;
        public static double dither_db = 0.0;
        public static int bright_adj = 0;
        public static int contrast_adj = 0;
        public static int total_adj = 0;

        // RGB palette, with black put at 0 and grays moved down
        // 13 x 4 = 52 colors x 3 = 156
        // palette from FBX, FirebrandX Smooth NES palette
        public static int[] NES_PALETTE = new int[156] {
            0,0,0,
            0x00, 0x13, 0x80,
            0x1e, 0x00, 0x8a,
            0x39, 0x00, 0x7a,
            0x55, 0x00, 0x56,
            0x5a, 0x00, 0x18,
            0x4f, 0x10, 0x00,
            0x3d, 0x1c, 0x00,
            0x25, 0x32, 0x00,
            0x00, 0x3d, 0x00,
            0x00, 0x40, 0x00,
            0x00, 0x39, 0x24,
            0x00, 0x2e, 0x55,

            0x6a, 0x6d, 0x6a,
            0x18, 0x50, 0xc7,
            0x4b, 0x30, 0xe3,
            0x73, 0x22, 0xd6,
            0x95, 0x1f, 0xa9,
            0x9d, 0x28, 0x5c,
            0x98, 0x37, 0x00,
            0x7f, 0x4c, 0x00,
            0x5e, 0x64, 0x00,
            0x22, 0x77, 0x00,
            0x02, 0x7e, 0x02,
            0x00, 0x76, 0x45,
            0x00, 0x6e, 0x8a,

            0xb9, 0xbc, 0xb9,
            0x68, 0xa6, 0xff,
            0x8c, 0x9c, 0xff,
            0xb5, 0x86, 0xff,
            0xd9, 0x75, 0xfd,
            0xe3, 0x77, 0xb9,
            0xe5, 0x8d, 0x68,
            0xd4, 0x9d, 0x29,
            0xb3, 0xaf, 0x0c,
            0x7b, 0xc2, 0x11,
            0x55, 0xca, 0x47,
            0x46, 0xcb, 0x81,
            0x47, 0xc1, 0xc5,

            0xff, 0xff, 0xff,
            0xcc, 0xea, 0xff,
            0xdd, 0xde, 0xff,
            0xec, 0xda, 0xff,
            0xf8, 0xd7, 0xfe,
            0xfc, 0xd6, 0xf5,
            0xfd, 0xdb, 0xcf,
            0xf9, 0xe7, 0xb5,
            0xf1, 0xf0, 0xaa,
            0xda, 0xfa, 0xa9,
            0xc9, 0xff, 0xbc,
            0xc3, 0xfb, 0xd7,
            0xc4, 0xf6, 0xf6
        };
        
        //public static int[] Out_Indexes = new int[4] { 0, 1, 2, 3 };
        public static Color sel_color = Color.Black;
        public static Color color1 = Color.Black;
        public static Color color2 = Color.Black;
        public static Color color3 = Color.Black;
        public static Color color4 = Color.Black;
        public static Color color5 = Color.Black; // extra color, why not
        public static int color1val, color2val, color3val, color4val, sel_color_val;
        public static int has_loaded = 0;
        public static int has_converted = 0;

        public static Bitmap work_bmp = new Bitmap(256, 256);
        public static Bitmap revert_bmp = new Bitmap(256, 256);
        public static Bitmap bright_bmp = new Bitmap(256, 256); // brightness adjust
        public static Bitmap conv_bmp = new Bitmap(256, 256);
        public static Bitmap left_bmp = new Bitmap(256, 256);
        public static Bitmap right_bmp = new Bitmap(256, 256); // dither scratchpad

        const int MAX_WIDTH = 256;
        const int MAX_HEIGHT = 240;
        public static int image_width, image_height;
        public static int user_width = 256;
        public static int user_height = 240;
        public static int remember_index;
        public static int[] Out_Array = new int[65536]; // for CHR output
        public static int[] CHR_All = new int[16384]; // 16 bytes * 256 tiles * 4
        public static int[] CHR_Reduced = new int[16384];
        public static int[] CHR_16bytes = new int[16];
        public static int[] Nametable = new int[1024]; // 32 x 30 (and some extra)

        // for auto color generator
        public static int[] Count_Array = new int[52]; // 65536 count each color
        public static int color_count; // how many total different colors
        public static int r_val, g_val, b_val, diff_val;
        public static int c_offset, c_offset2;

        public static int count_tiles, count_tiles2; // 2 is duplicate tiles removed
        public static int tile_offset, nametable_index;
        public static int out_size;
        public static int remember_width, remember_height;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = 1;
            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            label3.Focus();
            this.ActiveControl = label3;
        }


        private void pictureBox2_Click(object sender, EventArgs e)
        { // the NES palette box
            // grab a color from it
            var mouseEventArgs = e as MouseEventArgs;
            if (mouseEventArgs == null)
            {
                label3.Focus();
                return;
            }
            int map_x = mouseEventArgs.X;
            int map_y = mouseEventArgs.Y;
            if ((map_x < 0) || (map_x >= 208) ||
                (map_y < 0) || (map_y >= 64))
            {
                label3.Focus();
                return;
            }

            map_x = map_x >> 4;
            map_y = map_y >> 4;
            int final_color = (map_y * 13) + map_x;
            int index = final_color * 3;
            int final_r = NES_PALETTE[index];
            index++;
            int final_g = NES_PALETTE[index];
            index++;
            int final_b = NES_PALETTE[index];

            remember_index = final_color;
            string nes_str = GetNesVal(remember_index);
            label13.Text = nes_str;

            sel_color = Color.FromArgb(final_r, final_g, final_b);
            pictureBox7.BackColor = sel_color;
            sel_color_val = remember_index;
            /*label14.Text = sel_color.R.ToString() + ", " +
                sel_color.G.ToString() + ", " +
                sel_color.B.ToString();*/

            label3.Focus();
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        { // color 1
            MouseEventArgs me = (MouseEventArgs)e;
            if (me.Button == MouseButtons.Right) // right click
            {
                sel_color_val = color1val;
                sel_color = color1;
                /*label14.Text = sel_color.R.ToString() + ", " +
                    sel_color.G.ToString() + ", " +
                    sel_color.B.ToString();*/
                string nes_str = GetNesVal(sel_color_val);
                label13.Text = nes_str;
                pictureBox7.BackColor = sel_color;
            }
            else // left click
            {
                color1val = sel_color_val;
                color1 = sel_color;
                pictureBox3.BackColor = color1;
                label9.Text = label13.Text;

                /*label15.Text = color1.R.ToString() + ", " +
                    color1.G.ToString() + ", " +
                    color1.B.ToString();*/

                reconvert();
            }

            label3.Focus();
        }

        private void pictureBox4_Click(object sender, EventArgs e)
        { // color 2
            MouseEventArgs me = (MouseEventArgs)e;
            if (me.Button == MouseButtons.Right) // right click
            {
                sel_color_val = color2val;
                sel_color = color2;
                /*label14.Text = sel_color.R.ToString() + ", " +
                    sel_color.G.ToString() + ", " +
                    sel_color.B.ToString();*/
                string nes_str = GetNesVal(sel_color_val);
                label13.Text = nes_str;
                pictureBox7.BackColor = sel_color;
            }
            else  // left click
            {
                color2val = sel_color_val;
                color2 = sel_color;
                pictureBox4.BackColor = color2;
                label10.Text = label13.Text;

                /*label16.Text = color2.R.ToString() + ", " +
                    color2.G.ToString() + ", " +
                    color2.B.ToString();*/

                reconvert();
            }

            label3.Focus();
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        { // color 3
            MouseEventArgs me = (MouseEventArgs)e;
            if (me.Button == MouseButtons.Right) // right click
            {
                sel_color_val = color3val;
                sel_color = color3;
                /*label14.Text = sel_color.R.ToString() + ", " +
                    sel_color.G.ToString() + ", " +
                    sel_color.B.ToString();*/
                string nes_str = GetNesVal(sel_color_val);
                label13.Text = nes_str;
                pictureBox7.BackColor = sel_color;
            }
            else  // left click
            {
                color3val = sel_color_val;
                color3 = sel_color;
                pictureBox5.BackColor = color3;
                label11.Text = label13.Text;

                /*label17.Text = color3.R.ToString() + ", " +
                    color3.G.ToString() + ", " +
                    color3.B.ToString();*/

                reconvert();
            }

            label3.Focus();
        }

        private void pictureBox6_Click(object sender, EventArgs e)
        { // color 4
            MouseEventArgs me = (MouseEventArgs)e;
            if (me.Button == MouseButtons.Right) // right click
            {
                sel_color_val = color4val;
                sel_color = color4;
                /*label14.Text = sel_color.R.ToString() + ", " +
                    sel_color.G.ToString() + ", " +
                    sel_color.B.ToString();*/
                string nes_str = GetNesVal(sel_color_val);
                label13.Text = nes_str;
                pictureBox7.BackColor = sel_color;
            }
            else  // left click
            {
                color4val = sel_color_val;
                color4 = sel_color;
                pictureBox6.BackColor = color4;
                label12.Text = label13.Text;

                /*label18.Text = color4.R.ToString() + ", " +
                    color4.G.ToString() + ", " +
                    color4.B.ToString();*/

                reconvert();
            }

            label3.Focus();
        }

        private void button1_Click(object sender, EventArgs e)
        { // convert button
            if (has_loaded == 0)
            {
                label3.Focus();
                return;
            }

            do_convert();

            label3.Focus();
        }



        public void reconvert()
        {
            if (has_loaded == 0) return;

            if (has_converted == 0) return;

            do_convert();
        }



        public void do_convert()
        {
            if (has_loaded == 0) return;

            has_converted = 1;

            label6.Text = "Converted";

            int red, green, blue, bayer_val;
            int red_dif, green_dif, blue_dif;

            // blank the out array (CHR indexes)
            for (int i = 0; i < 65536; i++)
            {
                Out_Array[i] = 0;
            }

            // blank

            for (int xx = 0; xx < MAX_WIDTH; xx++)
            {
                for (int yy = 0; yy < MAX_HEIGHT; yy++)
                {
                    conv_bmp.SetPixel(xx, yy, Color.Black);
                }
            }


            

            Color tempcolor = Color.Black;
            Color tempcolor2 = Color.Black;

            // make sure work is bright adjusted

            dither_db = dither_factor / 10.0;
            dither_adjust = (int)(dither_db * 32.0);

            //copy orig to right_bmp, dither on it.
            if (comboBox1.SelectedIndex == FLOYD_STEIN)
            {
                //right_bmp
                for (int yy = 0; yy < image_height; yy++)
                {
                    for (int xx = 0; xx < image_width; xx++)
                    {
                        // do the dither later
                        tempcolor = bright_bmp.GetPixel(xx, yy);
                        right_bmp.SetPixel(xx, yy, tempcolor);
                    }
                }
            }
            else if (comboBox1.SelectedIndex == BAYER8)// BAYER8
            {
                // do the dither now
                for (int yy = 0; yy < image_height; yy++)
                {
                    for (int xx = 0; xx < image_width; xx++)
                    {
                        if (dither_factor > 0)
                        {
                            tempcolor = bright_bmp.GetPixel(xx, yy);
                            red = tempcolor.R - dither_adjust; // keep it from lightening
                            green = tempcolor.G - dither_adjust;
                            blue = tempcolor.B - dither_adjust;
                            bayer_val = BAYER_MATRIX[xx % 8, yy % 8];
                            bayer_val = (int)((double)bayer_val * dither_db);
                            red += bayer_val;
                            red = Math.Max(0, red); // clamp min max
                            red = Math.Min(255, red);
                            green += bayer_val;
                            green = Math.Max(0, green);
                            green = Math.Min(255, green);
                            blue += bayer_val;
                            blue = Math.Max(0, blue);
                            blue = Math.Min(255, blue);
                            right_bmp.SetPixel(xx, yy, Color.FromArgb(red, green, blue));
                        }
                        else
                        { // no dither factor
                            tempcolor = bright_bmp.GetPixel(xx, yy);
                            right_bmp.SetPixel(xx, yy, tempcolor);
                        }

                    }
                }
            }
            else if (comboBox1.SelectedIndex == SCANLINE)
            {
                dither_adjust = (dither_adjust * 2) / 3; // 2/3
                for (int yy = 0; yy < image_height; yy++)
                {
                    for (int xx = 0; xx < image_width; xx++)
                    {
                        tempcolor = bright_bmp.GetPixel(xx, yy);
                        red = tempcolor.R;
                        green = tempcolor.G;
                        blue = tempcolor.B;
                        if ((yy % 2) == 0)
                        {
                            red += dither_adjust;
                            green += dither_adjust;
                            blue += dither_adjust;
                        }
                        else
                        {
                            red -= dither_adjust;
                            green -= dither_adjust;
                            blue -= dither_adjust;
                        }
                        red = Math.Max(0, red); // clamp min max
                        red = Math.Min(255, red);
                        green = Math.Max(0, green);
                        green = Math.Min(255, green);
                        blue = Math.Max(0, blue);
                        blue = Math.Min(255, blue);
                        right_bmp.SetPixel(xx, yy, Color.FromArgb(red, green, blue));
                    }
                }
            }
            else if (comboBox1.SelectedIndex == BAYER2)
            {
                //
                for (int yy = 0; yy < image_height; yy++)
                {
                    for (int xx = 0; xx < image_width; xx++)
                    {
                        if (dither_factor > 0)
                        {
                            tempcolor = bright_bmp.GetPixel(xx, yy);
                            red = tempcolor.R;
                            green = tempcolor.G;
                            blue = tempcolor.B;
                            bayer_val = BAD_MATRIX[xx % 2, yy % 2];
                            bayer_val = (int)((double)bayer_val * dither_db);
                            red += bayer_val;
                            red = Math.Max(0, red); // clamp min max
                            red = Math.Min(255, red);
                            green += bayer_val;
                            green = Math.Max(0, green);
                            green = Math.Min(255, green);
                            blue += bayer_val;
                            blue = Math.Max(0, blue);
                            blue = Math.Min(255, blue);
                            right_bmp.SetPixel(xx, yy, Color.FromArgb(red, green, blue));
                        }
                        else
                        { // no dither factor
                            tempcolor = bright_bmp.GetPixel(xx, yy);
                            right_bmp.SetPixel(xx, yy, tempcolor);
                        }
                    }
                }
            }
            else // TRIANGLE
            {
                for (int yy = 0; yy < image_height; yy++)
                {
                    for (int xx = 0; xx < image_width; xx++)
                    {
                        if (dither_factor > 0)
                        {
                            tempcolor = bright_bmp.GetPixel(xx, yy);
                            red = tempcolor.R;
                            green = tempcolor.G;
                            blue = tempcolor.B;
                            bayer_val = TRI_MATRIX[yy % 4, xx % 4];
                            bayer_val = (int)((double)bayer_val * dither_db);
                            red += bayer_val;
                            red = Math.Max(0, red); // clamp min max
                            red = Math.Min(255, red);
                            green += bayer_val;
                            green = Math.Max(0, green);
                            green = Math.Min(255, green);
                            blue += bayer_val;
                            blue = Math.Max(0, blue);
                            blue = Math.Min(255, blue);
                            right_bmp.SetPixel(xx, yy, Color.FromArgb(red, green, blue));
                        }
                        else
                        { // no dither factor
                            tempcolor = bright_bmp.GetPixel(xx, yy);
                            right_bmp.SetPixel(xx, yy, tempcolor);
                        }
                    }
                }
            }


            dither_db = dither_factor / 12.0; // 10 seemed too much

            for (int yy = 0; yy < image_height; yy++)
            {
                for (int xx = 0; xx < image_width; xx++)
                {
                    // find the closest of the 4 colors
                    tempcolor = right_bmp.GetPixel(xx, yy);

                    tempcolor2 = Find_Best(tempcolor);

                    // find best sets remember_index 0-3
                    // for CHR output later
                    Out_Array[(yy * 256) + xx] = remember_index;

                    conv_bmp.SetPixel(xx, yy, tempcolor2);


                    if ((comboBox1.SelectedIndex == FLOYD_STEIN) && (dither_factor != 0))
                    {
                        // do the dither stuff
                        red_dif = tempcolor.R - tempcolor2.R;
                        green_dif = tempcolor.G - tempcolor2.G;
                        blue_dif = tempcolor.B - tempcolor2.B;
                        do_dither(red_dif, green_dif, blue_dif, xx, yy);
                    }
                }
            }


            // copy right_bmp to conv_bmp
            pictureBox1.Image = conv_bmp;

            // process the CHR array also
            Big_CHR_Loops();
        }


        public void do_dither(int red, int green, int blue, int xx, int yy)
        {
            // floyd steinburg dithering method
            // push the error to
            // -    0    7/16
            // 3/16 5/16 1/16

            double red_db = (double)red;
            double green_db = (double)green;
            double blue_db = (double)blue;

            Color tempcolor = Color.Black;

            //dither_db is a global, already set

            // right side = 7/16
            if (xx < image_width - 1)
            {
                tempcolor = right_bmp.GetPixel(xx + 1, yy);
                red = (int)((red_db * dither_db * 0.4375) + tempcolor.R);
                red = Math.Max(0, red); // clamp min max
                red = Math.Min(255, red);
                green = (int)((green_db * dither_db * 0.4375) + tempcolor.G);
                green = Math.Max(0, green); // clamp min max
                green = Math.Min(255, green);
                blue = (int)((blue_db * dither_db * 0.4375) + tempcolor.B);
                blue = Math.Max(0, blue); // clamp min max
                blue = Math.Min(255, blue);
                tempcolor = Color.FromArgb(red, green, blue);
                right_bmp.SetPixel(xx + 1, yy, tempcolor);
            }
            
            // below
            if (yy < image_height - 1)
            {
                if(xx > 0)
                { // below left = 3/16
                    tempcolor = right_bmp.GetPixel(xx - 1, yy + 1);
                    red = (int)((red_db * dither_db * 0.1875) + tempcolor.R);
                    red = Math.Max(0, red); // clamp min max
                    red = Math.Min(255, red);
                    green = (int)((green_db * dither_db * 0.1875) + tempcolor.G);
                    green = Math.Max(0, green); // clamp min max
                    green = Math.Min(255, green);
                    blue = (int)((blue_db * dither_db * 0.1875) + tempcolor.B);
                    blue = Math.Max(0, blue); // clamp min max
                    blue = Math.Min(255, blue);
                    tempcolor = Color.FromArgb(red, green, blue);
                    right_bmp.SetPixel(xx - 1, yy + 1, tempcolor);
                }

                // below = 5/16
                tempcolor = right_bmp.GetPixel(xx, yy + 1);
                red = (int)((red_db * dither_db * 0.3125) + tempcolor.R);
                red = Math.Max(0, red); // clamp min max
                red = Math.Min(255, red);
                green = (int)((green_db * dither_db * 0.3125) + tempcolor.G);
                green = Math.Max(0, green); // clamp min max
                green = Math.Min(255, green);
                blue = (int)((blue_db * dither_db * 0.3125) + tempcolor.B);
                blue = Math.Max(0, blue); // clamp min max
                blue = Math.Min(255, blue);
                tempcolor = Color.FromArgb(red, green, blue);
                right_bmp.SetPixel(xx, yy + 1, tempcolor);

                if(xx < image_width - 1)
                { // below right = 1/16
                    tempcolor = right_bmp.GetPixel(xx + 1, yy + 1);
                    red = (int)((red_db * dither_db * 0.0625) + tempcolor.R);
                    red = Math.Max(0, red); // clamp min max
                    red = Math.Min(255, red);
                    green = (int)((green_db * dither_db * 0.0625) + tempcolor.G);
                    green = Math.Max(0, green); // clamp min max
                    green = Math.Min(255, green);
                    blue = (int)((blue_db * dither_db * 0.0625) + tempcolor.B);
                    blue = Math.Max(0, blue); // clamp min max
                    blue = Math.Min(255, blue);
                    tempcolor = Color.FromArgb(red, green, blue);
                    right_bmp.SetPixel(xx + 1, yy + 1, tempcolor);
                }
            }
        }

        Color Find_Best(Color tempcolor)
        {
            int best_index = 0;
            int lowest_diff = 999999;
            int dR, dG, dB, color_diff, d_bright;

            dR = tempcolor.R - color1.R;
            dG = tempcolor.G - color1.G;
            dB = tempcolor.B - color1.B;
            d_bright = (Math.Abs(dR) + Math.Abs(dG) + Math.Abs(dB)) / 3;

            color_diff = (dR * dR) + (dG * dG) + (dB * dB) + (d_bright * d_bright);
            
            if (color_diff < lowest_diff)
            {
                lowest_diff = color_diff;
            }

            dR = tempcolor.R - color2.R;
            dG = tempcolor.G - color2.G;
            dB = tempcolor.B - color2.B;
            d_bright = (Math.Abs(dR) + Math.Abs(dG) + Math.Abs(dB)) / 3;
            // I changed this to fix an issue of some colors not being
            // chosen at all. This adds brightness as a factor.

            color_diff = (dR * dR) + (dG * dG) + (dB * dB) + (d_bright * d_bright);

            if (color_diff < lowest_diff)
            {
                lowest_diff = color_diff;
                best_index = 1;
            }

            dR = tempcolor.R - color3.R;
            dG = tempcolor.G - color3.G;
            dB = tempcolor.B - color3.B;
            d_bright = (Math.Abs(dR) + Math.Abs(dG) + Math.Abs(dB)) / 3;
            
            color_diff = (dR * dR) + (dG * dG) + (dB * dB) + (d_bright * d_bright);

            if (color_diff < lowest_diff)
            {
                lowest_diff = color_diff;
                best_index = 2;
            }

            dR = tempcolor.R - color4.R;
            dG = tempcolor.G - color4.G;
            dB = tempcolor.B - color4.B;
            d_bright = (Math.Abs(dR) + Math.Abs(dG) + Math.Abs(dB)) / 3;

            color_diff = (dR * dR) + (dG * dG) + (dB * dB) + (d_bright * d_bright);

            if (color_diff < lowest_diff)
            {
                lowest_diff = color_diff;
                best_index = 3;
            }

            remember_index = best_index;

            if (best_index == 0) return color1;
            if (best_index == 1) return color2;
            if (best_index == 2) return color3;
            return color4;
        }


        
        public void Big_CHR_Loops()
        {
            
            // erase the arrays
            for (int i = 0; i < 16384; i++)
            {
                CHR_All[i] = 0;
                CHR_Reduced[i] = 0;
            }


            // do each 128x128 block separately
            // always do the top left 128x128
            int temp_offset = 0;
            c_offset2 = 0;
            count_tiles = 256;
            for (int y = 0; y < 128; y += 8)
            {
                for (int x = 0; x < 128; x += 8)
                {
                    // process each 8x8 tile separately
                    Dry_CHR_Loop(x, y);

                    // have all 16 bytes, save them to file
                    for (int i = 0; i < 16; i++)
                    {
                        //fs.WriteByte((byte)CHR_16bytes[i]);
                        CHR_All[temp_offset++] = CHR_16bytes[i];
                    }
                }
            }
            // top right
            if (image_width > 128)
            {
                c_offset2 = 128;
                count_tiles += 256;

                for (int y = 0; y < 128; y += 8)
                {
                    for (int x = 0; x < 128; x += 8)
                    {
                        // process each 8x8 tile separately
                        Dry_CHR_Loop(x, y);

                        // have all 16 bytes, save them to file
                        for (int i = 0; i < 16; i++)
                        {
                            //fs.WriteByte((byte)CHR_16bytes[i]);
                            CHR_All[temp_offset++] = CHR_16bytes[i];
                        }
                    }
                }
            }
            // bottom left
            if (image_height > 128)
            {
                c_offset2 = 32768;
                count_tiles += 256;

                for (int y = 0; y < 128; y += 8)
                {
                    for (int x = 0; x < 128; x += 8)
                    {
                        // process each 8x8 tile separately
                        Dry_CHR_Loop(x, y);

                        // have all 16 bytes, save them to file
                        for (int i = 0; i < 16; i++)
                        {
                            //fs.WriteByte((byte)CHR_16bytes[i]);
                            CHR_All[temp_offset++] = CHR_16bytes[i];
                        }
                    }
                }
            }
            // bottom right
            if ((image_width > 128) && (image_height > 128))
            {
                c_offset2 = 128 + 32768;
                count_tiles += 256;

                for (int y = 0; y < 128; y += 8)
                {
                    for (int x = 0; x < 128; x += 8)
                    {
                        // process each 8x8 tile separately
                        Dry_CHR_Loop(x, y);

                        // have all 16 bytes, save them to file
                        for (int i = 0; i < 16; i++)
                        {
                            //fs.WriteByte((byte)CHR_16bytes[i]);
                            CHR_All[temp_offset++] = CHR_16bytes[i];
                        }
                    }
                }
            }

            label21.Text = count_tiles.ToString();

            // now remove duplicate tiles
            
            tile_offset = 16; // skip the first tile
            count_tiles2 = 1;
            int count16 = 1;
            nametable_index = 1;

            // just copy the first tile
            for (int i = 0; i < 16; i++)
            {
                CHR_Reduced[i] = CHR_All[i];
            }
            // Nametable[0] = 0; ...
            // just blank them all
            for(int i = 0; i < 960; i++)
            {
                Nametable[i] = 0;
            }

            // top left of image
            // skip the first tile
            for (int i = 1; i < 256; i++) // tiles in CHR_All
            {
                if (count16 == 16)
                {
                    count16 = 0;
                    nametable_index += 16;
                }

                bool unique_tile = true;
                for(int j = 0; j < count_tiles2; j++) // tiles in CHR_Reduced
                {
                    // check to see if the tile already exists in CHR_Reduced
                    if (compare_one(i,j) == true)
                    { // tiles are same
                        unique_tile = false;
                        Nametable[nametable_index] = j;
                        break;
                    }
                }
                if(unique_tile == true)
                {
                    copy_one(i);
                    Nametable[nametable_index] = count_tiles2;
                    count_tiles2++;
                }
                count16++;
                nametable_index++;
            }

            
            // top right
            if (image_width > 128)
            {
                count16 = 0;
                nametable_index = 16;
                for (int i = 256; i < 512; i++) // tiles in CHR_All
                {
                    if (count16 == 16)
                    {
                        count16 = 0;
                        nametable_index += 16;
                    }

                    bool unique_tile = true;
                    for (int j = 0; j < count_tiles2; j++) // tiles in CHR_Reduced
                    {
                        // check to see if the tile already exists in CHR_Reduced
                        if (compare_one(i, j) == true)
                        { // tiles are same
                            unique_tile = false;
                            Nametable[nametable_index] = j;
                            break;
                        }
                    }
                    if (unique_tile == true)
                    {
                        copy_one(i);
                        Nametable[nametable_index] = count_tiles2;
                        count_tiles2++;
                    }
                    count16++;
                    nametable_index++;
                }
            }
            
            // bottom left
            if (image_height > 128)
            {
                int start_tile = 256;
                int end_tile = 512;
                if(image_width > 128)
                {
                    start_tile = 512;
                    end_tile = 768;
                }
                count16 = 0;
                nametable_index = 512;
                for (int i = start_tile; i < end_tile; i++) // tiles in CHR_All
                {
                    if (count16 == 16)
                    {
                        count16 = 0;
                        nametable_index += 16;
                    }

                    bool unique_tile = true;
                    for (int j = 0; j < count_tiles2; j++) // tiles in CHR_Reduced
                    {
                        // check to see if the tile already exists in CHR_Reduced
                        if (compare_one(i, j) == true)
                        { // tiles are same
                            unique_tile = false;
                            Nametable[nametable_index] = j;
                            break;
                        }
                    }
                    if (unique_tile == true)
                    {
                        copy_one(i);
                        Nametable[nametable_index] = count_tiles2;
                        count_tiles2++;
                    }
                    count16++;
                    nametable_index++;
                }
            }

            // bottom right
            if ((image_width > 128) && (image_height > 128))
            {
                count16 = 0;
                nametable_index = 528;
                for (int i = 768; i < 1024; i++) // tiles in CHR_All
                {
                    if (count16 == 16)
                    {
                        count16 = 0;
                        nametable_index += 16;
                    }

                    bool unique_tile = true;
                    for (int j = 0; j < count_tiles2; j++) // tiles in CHR_Reduced
                    {
                        // check to see if the tile already exists in CHR_Reduced
                        if (compare_one(i, j) == true)
                        { // tiles are same
                            unique_tile = false;
                            Nametable[nametable_index] = j;
                            break;
                        }
                    }
                    if (unique_tile == true)
                    {
                        copy_one(i);
                        Nametable[nametable_index] = count_tiles2;
                        count_tiles2++;
                    }
                    count16++;
                    nametable_index++;
                }
            }
            // double check, the Attribute table is zero
            for(int i = 960; i < 1024; i++)
            {
                Nametable[i] = 0;
            }

            label22.Text = count_tiles2.ToString();
        }

        public bool compare_one(int i, int j)
        {
            int offset1 = i * 16;
            int offset2 = j * 16;
            for(int k = 0; k < 16; k++)
            {
                if(CHR_All[offset1] == CHR_Reduced[offset2])
                {
                    offset1++;
                    offset2++;
                }
                else
                {
                    return false; // tiles are different
                }
            }
            return true; // tiles are the same
        }

        public void copy_one(int tile_num)
        {
            // we found a unique tile, copy it to the CHR_Reduced array
            int offset3 = tile_num * 16;
            for(int i = 0; i < 16; i++)
            {
                CHR_Reduced[tile_offset++] = CHR_All[offset3++];
            }
        }

        public void Dry_CHR_Loop(int x, int y)
        { // common loop code
            //c_offset = CHR_Array offset
            //c_offset2 = 128x128 offset

            // Out_Array = new int[65536]; // for CHR output

            int index0, index8, temp_bits, bit1, bit2, CHR_byte; //, reorder;
            index0 = 0;
            index8 = 8;
            bit1 = 0;
            bit2 = 0;
            for (int y2 = 0; y2 < 8; y2++)
            {
                for (int x2 = 0; x2 < 8; x2++)
                {
                    c_offset = ((y + y2) * 256) + x + x2 + c_offset2;
                    CHR_byte = Out_Array[c_offset];
                    bit1 = CHR_byte & 1;
                    bit2 = CHR_byte & 2;
                    bit2 = bit2 >> 1;

                    temp_bits = CHR_16bytes[index0];
                    temp_bits = ((temp_bits << 1) + bit1) & 0xff;
                    CHR_16bytes[index0] = temp_bits;
                    temp_bits = CHR_16bytes[index8];
                    temp_bits = ((temp_bits << 1) + bit2) & 0xff;
                    CHR_16bytes[index8] = temp_bits;
                }
                index0++;
                index8++;
            }
        }


        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        { // exit
            // close the program
            Application.Exit();
        }

        private void importRGBToolStripMenuItem_Click(object sender, EventArgs e)
        { // import 12 bytes, 4 color x RGB
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Title = "Open palette RGB";
            openFileDialog1.Filter = "pal File (*.pal)|*.pal|bin File (*.bin)|*.bin|All files (*.*)|*.*";

            int red, green, blue;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                System.IO.FileStream fs = (System.IO.FileStream)openFileDialog1.OpenFile();
                if (fs.Length == 12)
                {
                    red = fs.ReadByte();
                    green = fs.ReadByte();
                    blue = fs.ReadByte();
                    color1 = Color.FromArgb(red, green, blue);
                    red = fs.ReadByte();
                    green = fs.ReadByte();
                    blue = fs.ReadByte();
                    color2 = Color.FromArgb(red, green, blue);
                    red = fs.ReadByte();
                    green = fs.ReadByte();
                    blue = fs.ReadByte();
                    color3 = Color.FromArgb(red, green, blue);
                    red = fs.ReadByte();
                    green = fs.ReadByte();
                    blue = fs.ReadByte();
                    color4 = Color.FromArgb(red, green, blue);

                    color_count = 4;
                    DRY_Palette(); // print numbers and color boxes
                }
                else
                {
                    MessageBox.Show("Error. Expected 12 byte file.");
                }

                fs.Close();
            }
            label3.Focus();
        }

        private void importNESToolStripMenuItem_Click(object sender, EventArgs e)
        { // import 4 bytes, NES indexes
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Title = "Open palette NES";
            openFileDialog1.Filter = "pal File (*.pal)|*.pal|bin File (*.bin)|*.bin|All files (*.*)|*.*";

            int val1, val2, val3, val4;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                System.IO.FileStream fs = (System.IO.FileStream)openFileDialog1.OpenFile();
                if ((fs.Length == 4) || (fs.Length == 16))
                {
                    val1 = fs.ReadByte();
                    val2 = fs.ReadByte();
                    val3 = fs.ReadByte();
                    val4 = fs.ReadByte();
                    
                    color1val = NEStoPaletteIndex(val1);
                    color2val = NEStoPaletteIndex(val2);
                    color3val = NEStoPaletteIndex(val3);
                    color4val = NEStoPaletteIndex(val4);

                    int index = color1val * 3;
                    int red = NES_PALETTE[index];
                    index++;
                    int green = NES_PALETTE[index];
                    index++;
                    int blue = NES_PALETTE[index];
                    color1 = Color.FromArgb(red, green, blue);

                    index = color2val * 3;
                    red = NES_PALETTE[index];
                    index++;
                    green = NES_PALETTE[index];
                    index++;
                    blue = NES_PALETTE[index];
                    color2 = Color.FromArgb(red, green, blue);

                    index = color3val * 3;
                    red = NES_PALETTE[index];
                    index++;
                    green = NES_PALETTE[index];
                    index++;
                    blue = NES_PALETTE[index];
                    color3 = Color.FromArgb(red, green, blue);

                    index = color4val * 3;
                    red = NES_PALETTE[index];
                    index++;
                    green = NES_PALETTE[index];
                    index++;
                    blue = NES_PALETTE[index];
                    color4 = Color.FromArgb(red, green, blue);

                    DRY_Palette(); // print numbers and color boxes
                }
                else
                {
                    MessageBox.Show("Error. Expected 4 or 16 byte file.");
                }

                fs.Close();
            }
            label3.Focus();
        }

        public int NEStoPaletteIndex(int inValue)
        {
            // I regret rearranging the palette colors.
            inValue = inValue & 0x3f; // force 0-63

            if (inValue < 0) return 0;
            if (inValue > 63) return 0;
            if (inValue == 0) return 13; // dark gray
            if (inValue < 13) return inValue;
            if (inValue < 16) return 0; // blacks
            if (inValue == 16) return 26; // light gray
            if (inValue < 29) return inValue - 3;
            if (inValue < 32) return 0; // blacks
            if (inValue == 32) return 39; // white
            if (inValue < 45) return inValue - 6;
            if (inValue == 45) return 13; // dark gray
            if (inValue < 48) return 0; // blacks
            if (inValue == 48) return 39; // white
            if (inValue < 61) return inValue - 9;
            if (inValue == 61) return 26; // light gray
            return 0; // blacks
        }

        private void exportRGBToolStripMenuItem_Click(object sender, EventArgs e)
        { // export 12 bytes, 4 color x RGB
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "pal File (*.pal)|*.pal|bin File (*.bin)|*.bin|All files (*.*)|*.*";
            saveFileDialog1.Title = "Save the palette RGB";
            saveFileDialog1.ShowDialog();

            if (saveFileDialog1.FileName != "")
            {
                System.IO.FileStream fs = (System.IO.FileStream)saveFileDialog1.OpenFile();
                fs.WriteByte(color1.R);
                fs.WriteByte(color1.G);
                fs.WriteByte(color1.B);
                fs.WriteByte(color2.R);
                fs.WriteByte(color2.G);
                fs.WriteByte(color2.B);
                fs.WriteByte(color3.R);
                fs.WriteByte(color3.G);
                fs.WriteByte(color3.B);
                fs.WriteByte(color4.R);
                fs.WriteByte(color4.G);
                fs.WriteByte(color4.B);
                
                fs.Close();
            }
            label3.Focus();
        }

        private void exportNESToolStripMenuItem_Click(object sender, EventArgs e)
        { // export 4 bytes, NES indexes
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "pal File (*.pal)|*.pal|bin File (*.bin)|*.bin|All files (*.*)|*.*";
            saveFileDialog1.Title = "Save the palette RGB";
            saveFileDialog1.ShowDialog();

            if (saveFileDialog1.FileName != "")
            {
                System.IO.FileStream fs = (System.IO.FileStream)saveFileDialog1.OpenFile();
                int NES_val = Pal_to_NES(color1val);
                fs.WriteByte((byte)NES_val);
                NES_val = Pal_to_NES(color2val);
                fs.WriteByte((byte)NES_val);
                NES_val = Pal_to_NES(color3val);
                fs.WriteByte((byte)NES_val);
                NES_val = Pal_to_NES(color4val);
                fs.WriteByte((byte)NES_val);

                fs.Close();
            }
            label3.Focus();
        }

        public int Pal_to_NES(int index)
        {
            // the index omits the xD, xE, xF colors
            // so the upper values are progressively off by 3
            if (index < 0) return 15; // 15 is black
            if (index > 51) return 15;
            if (index == 0) return 15; // black
            if (index < 13) return index;
            if (index == 13) return 0; // dark gray
            if (index < 26) return index + 3;
            if (index == 26) return 16; // light gray
            if (index < 39) return index + 6;
            if (index == 39) return 48; // white
            if (index < 52) return index + 9;
            return 0;
        }

        private void saveNESAsTXTToolStripMenuItem_Click(object sender, EventArgs e)
        { // copy text of the 4 bytes NES to clipboard
            string out_str = "";

            out_str = label9.Text;
            out_str += ", ";
            out_str += label10.Text;
            out_str += ", ";
            out_str += label11.Text;
            out_str += ", ";
            out_str += label12.Text;

            if (out_str != "")
            {
                Clipboard.SetDataObject(out_str);
            }
            label3.Focus();
        }

        private void button2_Click(object sender, EventArgs e)
        { // revert
            if (has_loaded == 0)
            {
                label3.Focus();
                return;
            }

            revert_image();

            label3.Focus();
        }


        public void Adjust_BMP()
        {
            //adjust the brightness and contrast

            Color temp_color = Color.Black;

            int bright_adj2 = bright_adj * 2;

            if (contrast_adj == 0) // no contrast adjustment
            {
                for (int xx = 0; xx < image_width; xx++)
                {
                    for (int yy = 0; yy < image_height; yy++)
                    {
                        temp_color = work_bmp.GetPixel(xx, yy);
                        int red = temp_color.R;
                        int green = temp_color.G;
                        int blue = temp_color.B;
                        red += bright_adj2;
                        red = Math.Max(0, red); // clamp min max
                        red = Math.Min(255, red);
                        green += bright_adj2;
                        green = Math.Max(0, green); // clamp min max
                        green = Math.Min(255, green);
                        blue += bright_adj2;
                        blue = Math.Max(0, blue); // clamp min max
                        blue = Math.Min(255, blue);
                        bright_bmp.SetPixel(xx, yy, Color.FromArgb(red, green, blue));
                    }
                }
            }
            if(contrast_adj > 0)
            {
                // increase contrast
                float contrastF = (float)contrast_adj / 16F;
                float tempF;

                for (int xx = 0; xx < image_width; xx++)
                {
                    for (int yy = 0; yy < image_height; yy++)
                    {
                        temp_color = work_bmp.GetPixel(xx, yy);
                        int red = temp_color.R;
                        int green = temp_color.G;
                        int blue = temp_color.B;
                        int total_val = (red + green + blue + 1) / 3;
                        total_val -= 128;
                        tempF = (float)total_val * contrastF;
                        total_val = (int)tempF;
                        
                        red += total_val; // contrast add value
                        red += bright_adj2;
                        red = Math.Max(0, red); // clamp min max
                        red = Math.Min(255, red);

                        green += total_val; // contrast add value
                        green += bright_adj2;
                        green = Math.Max(0, green); // clamp min max
                        green = Math.Min(255, green);

                        blue += total_val; // contrast add value
                        blue += bright_adj2;
                        blue = Math.Max(0, blue); // clamp min max
                        blue = Math.Min(255, blue);
                        bright_bmp.SetPixel(xx, yy, Color.FromArgb(red, green, blue));
                    }
                }
            }
            if(contrast_adj < 0)
            {
                // get closer to gray
                float multiplier1 = 0F - (float)contrast_adj; // was negative, now 0.5 - 100
                multiplier1 = multiplier1 / 100F; // now 0.005 - 1.00
                float multiplier2 = 1F - multiplier1; // 0.00 - 0.995
                float gray_val = 127F * multiplier1;
                float tempF;
                for (int xx = 0; xx < image_width; xx++)
                {
                    for (int yy = 0; yy < image_height; yy++)
                    {
                        temp_color = work_bmp.GetPixel(xx, yy);
                        int red = temp_color.R;
                        int green = temp_color.G;
                        int blue = temp_color.B;
                        tempF = (float)red * multiplier2;
                        tempF = (tempF + gray_val);
                        red = (int)tempF;
                        red += bright_adj2;
                        red = Math.Max(0, red); // clamp min max
                        red = Math.Min(255, red);

                        tempF = (float)green * multiplier2;
                        tempF = (tempF + gray_val);
                        green = (int)tempF;
                        green += bright_adj2;
                        green = Math.Max(0, green); // clamp min max
                        green = Math.Min(255, green);

                        tempF = (float)blue * multiplier2;
                        tempF = (tempF + gray_val);
                        blue = (int)tempF;
                        blue += bright_adj2;
                        blue = Math.Max(0, blue); // clamp min max
                        blue = Math.Min(255, blue);
                        bright_bmp.SetPixel(xx, yy, Color.FromArgb(red, green, blue));
                    }
                }
            }
            
        }

        public void Copy_2_Picbox()
        {
            Color temp_color = Color.Black;

            // copy pixel by pixel, work to left
            for (int xx = 0; xx < image_width; xx++)
            {
                for (int yy = 0; yy < image_height; yy++)
                {
                    temp_color = bright_bmp.GetPixel(xx, yy);
                    
                    left_bmp.SetPixel(xx, yy, temp_color);
                }
            }

            pictureBox1.Image = left_bmp;
            pictureBox1.Refresh();
        }

        public void revert_image()
        {
            has_converted = 0;
            label6.Text = "Loaded";

            label21.Text = "?";
            label22.Text = "?";

            // copy again, from revert to work
            Rectangle copyRect = new Rectangle(0, 0, image_width, image_height);
            using (Graphics g2 = Graphics.FromImage(work_bmp))
            {
                g2.DrawImage(revert_bmp, copyRect, copyRect, GraphicsUnit.Pixel);
            }

            Color temp_color = Color.Black;

            Adjust_BMP();

            Copy_2_Picbox();
        }

        private void button8_Click(object sender, EventArgs e)
        { // force grayscale
            if (has_loaded == 0)
            {
                label3.Focus();
                return;
            }
            has_converted = 0;
            label6.Text = "Loaded";

            label21.Text = "?";
            label22.Text = "?";

            // blank the left
            for (int xx = 0; xx < MAX_WIDTH; xx++)
            {
                for (int yy = 0; yy < MAX_HEIGHT; yy++)
                {
                    left_bmp.SetPixel(xx, yy, Color.Black);
                }
            }

            float redf, greenf, bluef;
            int total;
            Color tempcolor = Color.Black;

            // convert to grayscale
            for (int y = 0; y < image_height; y++)
            {
                for (int x = 0; x < image_width; x++)
                {
                    tempcolor = work_bmp.GetPixel(x, y);

                    // brightness adjust

                    int red = tempcolor.R;
                    int green = tempcolor.G;
                    int blue = tempcolor.B;
                    red += bright_adj;
                    red = Math.Max(0, red); // clamp min max
                    red = Math.Min(255, red);
                    green += bright_adj;
                    green = Math.Max(0, green); // clamp min max
                    green = Math.Min(255, green);
                    blue += bright_adj;
                    blue = Math.Max(0, blue); // clamp min max
                    blue = Math.Min(255, blue);

                    redf = red;
                    greenf = green;
                    bluef = blue;
                    total = (int)((0.3 * redf) + (0.59 * greenf) + (0.11 * bluef));
                    tempcolor = Color.FromArgb(total, total, total);
                    bright_bmp.SetPixel(x, y, tempcolor);
                    left_bmp.SetPixel(x, y, tempcolor); // copy to both
                }
            }


            // show in picture box
            pictureBox1.Image = left_bmp;
            pictureBox1.Refresh();


            color1 = Color.Black;
            color2 = Color.FromArgb(0x6a, 0x6d, 0x6a);
            color3 = Color.FromArgb(0xb9, 0xbc, 0xb9);
            color4 = Color.White;
            DRY_Palette();

            label3.Focus();
        }

        private void imageFromClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            paste_clipboard();
            label3.Focus();
        }


        public void blank_left_BMP()
        {
            for(int y = 0; y < MAX_HEIGHT; y++)
            {
                for (int x = 0; x < MAX_WIDTH; x++)
                {
                    left_bmp.SetPixel(x, y, Color.Black);
                }
            }
        }

        public void paste_clipboard()
        {
            IDataObject myClip = Clipboard.GetDataObject();

            if (myClip.GetDataPresent(DataFormats.Bitmap))
            {
                Bitmap temp_bmp = new Bitmap(256, 256);
                temp_bmp = myClip.GetData(DataFormats.Bitmap) as Bitmap;

                // NOTE this is identical to the Import Image code
                // any changes need to happen on both sets of code
                // lots of redundant code...
                // todo, make these 2 functions into 1 common function

                if ((temp_bmp.Height < 8) || (temp_bmp.Width < 8))
                {
                    MessageBox.Show("Error. File too small?");
                    temp_bmp.Dispose();
                    return;
                }

                has_loaded = 1;
                has_converted = 0;

                bright_adj = 0;
                contrast_adj = 0;
                textBox4.Text = "0";
                textBox5.Text = "0";

                float ratio1 = 1.0F;
                float ratio2 = 1.0F;
                int resize_width = user_width;
                int resize_height = user_height;
                int need_resize = 0;

                // clear it
                for (int y = 0; y < MAX_HEIGHT; y++)
                {
                    for (int x = 0; x < MAX_WIDTH; x++)
                    {
                        work_bmp.SetPixel(x, y, Color.Black);
                    }
                }

                if (temp_bmp.Width > user_width)
                {
                    image_width = user_width;
                    ratio1 = temp_bmp.Width / (float)user_width;
                    need_resize = 1;
                }
                else
                {
                    image_width = temp_bmp.Width;
                }

                if (temp_bmp.Height > user_height)
                {
                    image_height = user_height;
                    ratio2 = temp_bmp.Height / (float)user_height;
                    need_resize = 1;
                }
                else
                {
                    image_height = temp_bmp.Height;
                }

                // copy the bitmap
                if ((checkBox1.Checked == true) && (need_resize == 1))
                {
                    // which is bigger? divide by that
                    if (ratio1 > ratio2)
                    {
                        resize_width = (int)Math.Round(temp_bmp.Width / ratio1);
                        resize_height = (int)Math.Round(temp_bmp.Height / ratio1);
                    }
                    else
                    {
                        resize_width = (int)Math.Round(temp_bmp.Width / ratio2);
                        resize_height = (int)Math.Round(temp_bmp.Height / ratio2);
                    }

                    // resize to fit
                    using (Graphics g2 = Graphics.FromImage(work_bmp))
                    {
                        g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g2.DrawImage(temp_bmp, 0, 0, resize_width, resize_height);
                    }

                    image_width = resize_width;
                    image_height = resize_height;
                }
                else
                {
                    // copy the bitmap, crop but don't resize
                    Rectangle copyRect = new Rectangle(0, 0, image_width, image_height);
                    using (Graphics g2 = Graphics.FromImage(work_bmp))
                    {
                        g2.DrawImage(temp_bmp, copyRect, copyRect, GraphicsUnit.Pixel);
                    }


                }

                // copy again
                Rectangle copyRect2 = new Rectangle(0, 0, image_width, image_height);
                using (Graphics g2 = Graphics.FromImage(revert_bmp))
                {
                    g2.DrawImage(work_bmp, copyRect2, copyRect2, GraphicsUnit.Pixel);
                }


                // round down to nearest 8 ?
                remember_width = image_width;
                remember_height = image_height;
                if (checkBox3.Checked == true)
                {
                    image_width = image_width & 0xfff8;
                    image_height = image_height & 0xfff8;
                }


                blank_left_BMP();

                Adjust_BMP(); // bright adjust

                Copy_2_Picbox();


                // show the width and height
                label4.Text = image_width.ToString();
                label26.Text = image_height.ToString();

                label6.Text = "Loaded";
                label21.Text = "?";
                label22.Text = "?";
                temp_bmp.Dispose();
            }
            else
            {
                MessageBox.Show("Clipboard is not in bitmap format.");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        { // auto generate palette
            if (has_loaded == 0)
            {
                label3.Focus();
                return;
            }
            
            int times3;

            // default colors
            color1 = Color.Black;
            color2 = Color.Black;
            color3 = Color.Black;
            color4 = Color.Black;
            label9.Text = "$0f";
            label10.Text = "$0f";
            label11.Text = "$0f";
            label12.Text = "$0f";

            // blank the arrays
            for (int i = 0; i < 52; i++)
            {
                Count_Array[i] = 0;
            }
            color_count = 0;

            Color tempcolor = Color.Black;

            // count all NES colors
            for (int yy = 0; yy < image_height; yy++)
            {
                for (int xx = 0; xx < image_width; xx++)
                {
                    tempcolor = bright_bmp.GetPixel(xx, yy);

                    tempcolor = ToNES(tempcolor, -1);

                    Count_Array[remember_index] += 1;


                }
            }
            

            color_count = 0;
            // how many different colors
            for (int i = 0; i < 52; i++)
            {
                if (Count_Array[i] != 0) color_count++;
            }

            //label7.Text = color_count.ToString(); // print, how many colors


            // then reduce to 4 colors, using a plain merge
            // the closest neighbor color

            int color_count2 = color_count;
            while (color_count2 > 4)
            {
                //find the least count
                int least_index = 0;
                int least_cnt = 999999;
                for (int i = 0; i < 52; i++)
                {
                    if (Count_Array[i] == 0) continue;
                    if (Count_Array[i] < least_cnt)
                    {
                        least_cnt = Count_Array[i];
                        least_index = i;
                    }
                }
                // delete itself
                Count_Array[least_index] = 0;

                int closest_index = 0;
                int closest_val = 999999;
                times3 = least_index * 3;
                r_val = NES_PALETTE[times3];
                g_val = NES_PALETTE[times3+1];
                b_val = NES_PALETTE[times3+2];
                int dR = 0, dG = 0, dB = 0;

                // find the closest to that one
                for (int i = 0; i < 52; i++)
                {
                    if (Count_Array[i] == 0) continue;
                    times3 = i * 3;
                    dR = r_val - NES_PALETTE[times3];
                    dG = g_val - NES_PALETTE[times3+1];
                    dB = b_val - NES_PALETTE[times3+2];
                    diff_val = ((dR * dR) + (dG * dG) + (dB * dB));

                    if (diff_val < closest_val)
                    {
                        closest_val = diff_val;
                        closest_index = i;
                    }
                }
                if(color_count2 == 5) // save the 5th color, why not.
                {
                    color5 = Color.FromArgb(r_val, g_val, b_val);
                }
                
                Count_Array[closest_index] += least_cnt;

                color_count2--;

            }
            //label8.Text = color_count2.ToString(); // print, final # colors (4)

            // find the 4 colors
            
            int final_index = 0;
            times3 = 0;
            for (int i = 0; i < 52; i++)
            {
                if (Count_Array[i] == 0) continue;
                times3 = i * 3;
                r_val = NES_PALETTE[times3];
                g_val = NES_PALETTE[times3+1];
                b_val = NES_PALETTE[times3+2];
                tempcolor = Color.FromArgb(r_val, g_val, b_val);
                switch (final_index)
                {
                    case 0:
                        color1 = tempcolor;
                        break;
                    case 1:
                        color2 = tempcolor;
                        break;
                    case 2:
                        color3 = tempcolor;
                        break;
                    case 3:
                        color4 = tempcolor;
                        break;
                    default:
                        break;
                }

                final_index++;
            }


            // reorder white so it is at the bottom (right)
            /*if (color1.ToArgb() == Color.White.ToArgb() )
            {
                tempcolor = color1; // white
                color1 = color2;
                color2 = color3;
                color3 = color4;
                color4 = tempcolor;
            }
            else if (color2.ToArgb() == Color.White.ToArgb() )
            {
                tempcolor = color2; // white
                color2 = color3;
                color3 = color4;
                color4 = tempcolor;
            }
            else if (color3.ToArgb() == Color.White.ToArgb() )
            {
                tempcolor = color3; // white
                color3 = color4;
                color4 = tempcolor;
            }*/


            // order them dark to light

            int test_val1, test_val2;
            test_val1 = color1.R + color1.B + color1.G;
            test_val2 = color2.R + color2.B + color2.G;
            if(test_val1 > test_val2)
            {
                tempcolor = color1;
                color1 = color2;
                color2 = tempcolor;
            }
            test_val1 = color1.R + color1.B + color1.G;
            test_val2 = color3.R + color3.B + color3.G;
            if (test_val1 > test_val2)
            {
                tempcolor = color1;
                color1 = color3;
                color3 = tempcolor;
            }
            test_val1 = color1.R + color1.B + color1.G;
            test_val2 = color4.R + color4.B + color4.G;
            if (test_val1 > test_val2)
            {
                tempcolor = color1;
                color1 = color4;
                color4 = tempcolor;
            }
            test_val1 = color2.R + color2.B + color2.G;
            test_val2 = color3.R + color3.B + color3.G;
            if (test_val1 > test_val2)
            {
                tempcolor = color2;
                color2 = color3;
                color3 = tempcolor;
            }
            test_val1 = color2.R + color2.B + color2.G;
            test_val2 = color4.R + color4.B + color4.G;
            if (test_val1 > test_val2)
            {
                tempcolor = color2;
                color2 = color4;
                color4 = tempcolor;
            }
            test_val1 = color3.R + color3.B + color3.G;
            test_val2 = color4.R + color4.B + color4.G;
            if (test_val1 > test_val2)
            {
                tempcolor = color3;
                color3 = color4;
                color4 = tempcolor;
            }


            DRY_Palette(); // don't repeat yourself
            if(color_count > 4)
            { // try a 5th color, why not?
                color5 = ToNES(color5, -1); // to get the remember_index
                pictureBox7.BackColor = color5;
                sel_color = color5; // remember_index;
                sel_color_val = remember_index;
                /*label14.Text = sel_color.R.ToString() + ", " +
                    sel_color.G.ToString() + ", " +
                    sel_color.B.ToString();*/
                string nes_str = GetNesVal(remember_index);
                label13.Text = nes_str;
            }

            do_convert();

            label3.Focus();
        }

        public void DRY_Palette()
        {
            // convert all RGB to nearest NES color
            
            color1 = ToNES(color1, -1); // ToNES also sets remember_index
            string nes_str = GetNesVal(remember_index);
            label9.Text = nes_str;
            color1val = remember_index;

            //if (color_count == 1) goto End1;

            Color tempcolor = color2;
            color2 = ToNES(color2, -1);
            if(color1 == color2)
            { // bug fix, and also gives us more colors
                color2 = ToNES(tempcolor, remember_index);
            }
            nes_str = GetNesVal(remember_index);
            label10.Text = nes_str;
            color2val = remember_index;

            //if (color_count == 2) goto End1;

            tempcolor = color3;
            color3 = ToNES(color3, -1);
            if ((color1 == color3) || (color2 == color3))
            { // bug fix, and also gives us more colors
                color3 = ToNES(tempcolor, remember_index);
            }
            nes_str = GetNesVal(remember_index);
            label11.Text = nes_str;
            color3val = remember_index;

            //if (color_count == 3) goto End1;

            tempcolor = color4;
            color4 = ToNES(color4, -1);
            if ((color1 == color4) || (color2 == color4) || (color3 == color4))
            { // bug fix, and also gives us more colors
                color4 = ToNES(tempcolor, remember_index);
            }
            nes_str = GetNesVal(remember_index);
            label12.Text = nes_str;
            color4val = remember_index;

            //End1:

            // copy to the boxes
            pictureBox3.BackColor = color1;
            pictureBox4.BackColor = color2;
            pictureBox5.BackColor = color3;
            pictureBox6.BackColor = color4;

            // print the RGB
            /*label15.Text = color1.R.ToString() + ", " +
                color1.G.ToString() + ", " +
                color1.B.ToString();
            label16.Text = color2.R.ToString() + ", " +
                color2.G.ToString() + ", " +
                color2.B.ToString();
            label17.Text = color3.R.ToString() + ", " +
                color3.G.ToString() + ", " +
                color3.B.ToString();
            label18.Text = color4.R.ToString() + ", " +
                color4.G.ToString() + ", " +
                color4.B.ToString();*/
        }


        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        { // dither factor
            if (e.KeyChar == (char)Keys.Return)
            {
                //dither_factor_set();

                e.Handled = true; // prevent ding on return press

                label3.Focus(); // this calls the leave function
            }
        }

        private void textBox1_Leave(object sender, EventArgs e)
        { // dither factor
            dither_factor_set();
            label3.Focus();
        }

        public void dither_factor_set()
        {
            string str = textBox1.Text;
            int outvar = 0;
            if (int.TryParse(str, out outvar))
            {
                if (outvar > 12) outvar = 12;
                if (outvar < 0) outvar = 0;
                dither_factor = outvar;
                textBox1.Text = outvar.ToString();
            }
            else
            {
                // revert back to previous
                textBox1.Text = dither_factor.ToString();
            }

            reconvert();
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                u_width_set();

                e.Handled = true; // prevent ding on return press

                label3.Focus();
            }
        }

        public void u_width_set()
        {
            string str = textBox2.Text;
            int outvar = 0;
            if (int.TryParse(str, out outvar))
            {
                if (outvar > 256) outvar = 256;
                if (outvar < 8) outvar = 8;
                user_width = outvar;
                textBox2.Text = outvar.ToString();
            }
            else
            {
                // revert back to previous
                textBox2.Text = user_width.ToString();
            }
        }

        public void u_height_set()
        {
            string str = textBox3.Text;
            int outvar = 0;
            if (int.TryParse(str, out outvar))
            {
                if (outvar > 240) outvar = 240;
                if (outvar < 8) outvar = 8;
                user_height = outvar;
                textBox3.Text = outvar.ToString();
            }
            else
            {
                // revert back to previous
                textBox3.Text = user_height.ToString();
            }
        }

        private void textBox3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                u_height_set();

                e.Handled = true; // prevent ding on return press

                label3.Focus();
            }
        }

        private void saveRawCHRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (has_loaded == 0)
            {
                MessageBox.Show("Image hasn't loaded yet.");
                label3.Focus();
                return;
            }
            if (has_converted == 0)
            {
                MessageBox.Show("Image hasn't converted yet.");
                label3.Focus();
                return;
            }

            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "bin File (*.chr)|*.chr|All files (*.*)|*.*";
            saveFileDialog1.Title = "Save the CHR";
            saveFileDialog1.ShowDialog();

            if (saveFileDialog1.FileName != "")
            {
                System.IO.FileStream fs = (System.IO.FileStream)saveFileDialog1.OpenFile();

                int num_bytes = 16 * count_tiles;
                for (int i = 0; i < num_bytes; i++)
                {
                    fs.WriteByte((byte)CHR_All[i]);
                }

                fs.Close();
            }
            label3.Focus();
        }

        private void saveFinalCHRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (has_loaded == 0)
            {
                MessageBox.Show("Image hasn't loaded yet.");
                label3.Focus();
                return;
            }
            if (has_converted == 0)
            {
                MessageBox.Show("Image hasn't converted yet.");
                label3.Focus();
                return;
            }

            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "bin File (*.chr)|*.chr|DZ4 (*.dz4)|*.dz4";
            saveFileDialog1.Title = "Save the CHR";
            saveFileDialog1.ShowDialog();

            if (saveFileDialog1.FileName != "")
            {
                System.IO.FileStream fs = (System.IO.FileStream)saveFileDialog1.OpenFile();

                string ext = System.IO.Path.GetExtension(saveFileDialog1.FileName);

                int num_bytes = 16 * count_tiles2;
                if (checkBox2.Checked == true)
                { // pad up to nearest $1000
                    num_bytes += 0xfff;
                    num_bytes &= 0xf000;
                    if (num_bytes > 16384) num_bytes = 16384; // max
                }
                

                if (ext == ".chr")
                {
                    
                    for (int i = 0; i < num_bytes; i++)
                    {
                        fs.WriteByte((byte)CHR_Reduced[i]);
                    }

                    /*if (checkBox2.Checked == true) // pad to nearest $1000
                    {
                        num_bytes = num_bytes & 0xfff;
                        num_bytes = 0x1000 - num_bytes;
                        for (int i = 0; i < num_bytes; i++)
                        {
                            fs.WriteByte(0);
                        }
                    }*/
                }
                else // dz4 compressed
                {
                    out_size = num_bytes;
                    if (CompressIt(CHR_Reduced, out_size) == 1)
                    {
                        // now in rle_array, rle_size
                        for (int i = 0; i < rle_size; i++)
                        {
                            fs.WriteByte(rle_array[i]);
                        }
                    }
                    // if failed, it should have given a warning
                }
                

                fs.Close();
            }
            label3.Focus();
        }

        private void saveNametableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (has_loaded == 0)
            {
                MessageBox.Show("Image hasn't loaded yet.");
                label3.Focus();
                return;
            }
            if (has_converted == 0)
            {
                MessageBox.Show("Image hasn't converted yet.");
                label3.Focus();
                return;
            }

            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "bin File (*.nam)|*.nam|DZ4 (*.dz4)|*.dz4";
            saveFileDialog1.Title = "Save the Nametable";
            saveFileDialog1.ShowDialog();

            if (saveFileDialog1.FileName != "")
            {
                System.IO.FileStream fs = (System.IO.FileStream)saveFileDialog1.OpenFile();

                string ext = System.IO.Path.GetExtension(saveFileDialog1.FileName);

                if (ext == ".nam")
                {
                    for (int i = 0; i < 1024; i++)
                    {
                        fs.WriteByte((byte)Nametable[i]);
                        // NOTE - this will only work right for
                        // tile count <= 256 because it removes upper byte
                    }
                }
                else //dz4 compressed
                {
                    out_size = 1024;
                    if (CompressIt(Nametable, out_size) == 1)
                    {
                        // now in rle_array, rle_size
                        for (int i = 0; i < rle_size; i++)
                        {
                            fs.WriteByte(rle_array[i]);
                        }
                    }
                    // if failed, it should have given a warning
                }

                if (count_tiles2 > 256)
                {
                    MessageBox.Show("Warning. Over 256 unique tiles.");
                }

                fs.Close();
            }
            label3.Focus();
        }

        private void saveNES16BytesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "pal File (*.pal)|*.pal|bin File (*.bin)|*.bin|All files (*.*)|*.*";
            saveFileDialog1.Title = "Save the palette RGB";
            saveFileDialog1.ShowDialog();

            if (saveFileDialog1.FileName != "")
            {
                System.IO.FileStream fs = (System.IO.FileStream)saveFileDialog1.OpenFile();
                int NES_val = Pal_to_NES(color1val);
                int Main_val = NES_val;
                fs.WriteByte((byte)NES_val);
                NES_val = Pal_to_NES(color2val);
                fs.WriteByte((byte)NES_val);
                NES_val = Pal_to_NES(color3val);
                fs.WriteByte((byte)NES_val);
                NES_val = Pal_to_NES(color4val);
                fs.WriteByte((byte)NES_val);

                for(int i = 0; i < 3; i++)
                {
                    fs.WriteByte((byte)Main_val);
                    fs.WriteByte(0x0f); // black
                    fs.WriteByte(0x0f); // black
                    fs.WriteByte(0x0f); // black
                }

                fs.Close();
            }
            label3.Focus();
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            label3.Focus();
            // bug fix on the Leave events below...
            // they weren't firing on menu click events
        }



        public void colors_are_hard(int col_1, int col_2, int col_3, int col_4)
        {
            // in values are 4 NES palette #s 0-3f
            //converts it, and puts it in color1, color2, etc.

            color1val = NEStoPaletteIndex(col_1);
            int index = color1val * 3;
            int red = NES_PALETTE[index];
            index++;
            int green = NES_PALETTE[index];
            index++;
            int blue = NES_PALETTE[index];
            color1 = Color.FromArgb(red, green, blue);

            color2val = NEStoPaletteIndex(col_2);
            index = color2val * 3;
            red = NES_PALETTE[index];
            index++;
            green = NES_PALETTE[index];
            index++;
            blue = NES_PALETTE[index];
            color2 = Color.FromArgb(red, green, blue);

            color3val = NEStoPaletteIndex(col_3);
            index = color3val * 3;
            red = NES_PALETTE[index];
            index++;
            green = NES_PALETTE[index];
            index++;
            blue = NES_PALETTE[index];
            color3 = Color.FromArgb(red, green, blue);

            color4val = NEStoPaletteIndex(col_4);
            index = color4val * 3;
            red = NES_PALETTE[index];
            index++;
            green = NES_PALETTE[index];
            index++;
            blue = NES_PALETTE[index];
            color4 = Color.FromArgb(red, green, blue);

            //DRY_Palette();
            string nes_str = GetNesVal(color1val);
            label9.Text = nes_str;
            nes_str = GetNesVal(color2val);
            label10.Text = nes_str;
            nes_str = GetNesVal(color3val);
            label11.Text = nes_str;
            nes_str = GetNesVal(color4val);
            label12.Text = nes_str;

            // copy to the boxes
            pictureBox3.BackColor = color1;
            pictureBox4.BackColor = color2;
            pictureBox5.BackColor = color3;
            pictureBox6.BackColor = color4;

            // print the RGB
            /*label15.Text = color1.R.ToString() + ", " +
                color1.G.ToString() + ", " +
                color1.B.ToString();
            label16.Text = color2.R.ToString() + ", " +
                color2.G.ToString() + ", " +
                color2.B.ToString();
            label17.Text = color3.R.ToString() + ", " +
                color3.G.ToString() + ", " +
                color3.B.ToString();
            label18.Text = color4.R.ToString() + ", " +
                color4.G.ToString() + ", " +
                color4.B.ToString();*/

        }
        private void blueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x02, 0x12, 0x22, 0x32);

            do_convert();

            label3.Focus();
        }

        private void purpleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x04, 0x13, 0x23, 0x33);

            do_convert();

            label3.Focus();
        }

        private void pinkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x05, 0x15, 0x25, 0x35);

            do_convert();

            label3.Focus();
        }

        private void redToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x06, 0x16, 0x26, 0x36);

            do_convert();

            label3.Focus();
        }

        private void yellowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x07, 0x17, 0x27, 0x37);

            do_convert();

            label3.Focus();
        }

        private void greenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x09, 0x19, 0x29, 0x39);

            do_convert();

            label3.Focus();
        }

        private void oceanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x0c, 0x1c, 0x2c, 0x3c);

            do_convert();

            label3.Focus();
        }

        private void blue2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x02, 0x13, 0x24, 0x36);

            do_convert();

            label3.Focus();
        }

        private void purple2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x04, 0x15, 0x26, 0x37);

            do_convert();

            label3.Focus();
        }

        private void fireToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x07, 0x16, 0x27, 0x36);

            do_convert();

            label3.Focus();
        }

        private void forestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x07, 0x18, 0x29, 0x39);

            do_convert();

            label3.Focus();
        }

        private void mintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x0b, 0x1b, 0x2a, 0x39);

            do_convert();

            label3.Focus();
        }

        private void deepBlueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x0f, 0x0c, 0x1c, 0x22);

            do_convert();

            label3.Focus();
        }

        private void moodyToolStripMenuItem_Click(object sender, EventArgs e)
        { // blue purple
            colors_are_hard(0x04, 0x13, 0x22, 0x30);

            do_convert();

            label3.Focus();
        }

        /*private void grellowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x06, 0x19, 0x28, 0x30);

            do_convert();

            label3.Focus();
        }*/

        private void grayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x0f, 0x00, 0x10, 0x30);

            do_convert();

            label3.Focus();
        }

        private void textBox4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                //brightness_adjust();

                e.Handled = true; // prevent ding on return press

                label3.Focus(); // this calls the leave function
            }
        }

        private void textBox4_Leave(object sender, EventArgs e)
        {
            brightness_adjust();
            label3.Focus();
        }

        public void brightness_adjust()
        {
            string str = textBox4.Text;
            int outvar = 0;
            if (int.TryParse(str, out outvar))
            {
                if (outvar > 125) outvar = 125;
                if (outvar < -125) outvar = -125;
                bright_adj = outvar;
                textBox4.Text = outvar.ToString();
            }
            else
            {
                // revert back to previous
                textBox4.Text = bright_adj.ToString();
            }

            Adjust_BMP();
            reconvert();

            if ((has_loaded != 0) && (has_converted == 0))
            {
                revert_image(); // will redraw the image with the new brightness
            }
        }


        private void textBox5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                //brightness_adjust();

                e.Handled = true; // prevent ding on return press

                label3.Focus(); // this calls the leave function
            }
        }

        private void textBox5_Leave(object sender, EventArgs e)
        {
            contrast_adjust();
            label3.Focus();
        }

        public void contrast_adjust()
        {
            string str = textBox5.Text;
            int outvar = 0;
            if (int.TryParse(str, out outvar))
            {
                if (outvar > 200) outvar = 200;
                if (outvar < -95) outvar = -95;
                contrast_adj = outvar;
                textBox5.Text = outvar.ToString();
            }
            else
            {
                // revert back to previous
                textBox5.Text = contrast_adj.ToString();
            }

            Adjust_BMP();
            reconvert();

            if ((has_loaded != 0) && (has_converted == 0))
            {
                revert_image(); // will redraw the image with the new brightness
            }
        }



        private void whiteStripesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x0f, 0x16, 0x0f, 0x30);

            do_convert();

            label3.Focus();
        }

        private void sMonitorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            colors_are_hard(0x0f, 0x0f, 0x29, 0x0f);

            do_convert();

            label3.Focus();
        }

        private void save8bitIndexedBMPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // this is a specific thing that NES Screen Tool uses for imports
            // a Bitmap file, 8-bit, with indexed color (4 color)

            if (has_loaded == 0)
            {
                MessageBox.Show("No image loaded.");
                label3.Focus();
                return;
            }
            if (has_converted == 0)
            {
                MessageBox.Show("Image hasn't converted yet.");
                label3.Focus();
                return;
            }

            //conv_bmp no... use...
            //Out_Array[(yy * 256) + xx] = remember_index;


            // open dialogue
            // save file

            int image_width2 = (image_width + 15) & 0xfff0; // round up to nearest 16
            int image_height2 = (image_height + 15) & 0xfff0; // round up to nearest 16
            if (image_width2 < 32) image_width2 = 32; // just double checking max/min
            if (image_height2 < 32) image_height2 = 32;
            if (image_width2 > 256) image_width2 = 256;
            if (image_height2 > 256) image_height2 = 256;

            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "bmp File (*.bmp)|*.bmp|All files (*.*)|*.*";
            saveFileDialog1.Title = "Save the CHR";
            saveFileDialog1.ShowDialog();

            if (saveFileDialog1.FileName != "")
            {
                System.IO.FileStream fs = (System.IO.FileStream)saveFileDialog1.OpenFile();

                int img_size = image_width2 * image_height2;
                int file_size = img_size + 0x436;

                // write the header
                fs.WriteByte(0x42); // B
                fs.WriteByte(0x4d); // M

                fs.WriteByte((byte)file_size); // 4 bytes file size
                fs.WriteByte((byte)(file_size >> 8));
                fs.WriteByte((byte)(file_size >> 16));
                fs.WriteByte((byte)(file_size >> 24));

                fs.WriteByte(0); // 4 bytes, reserved, zero is fine
                fs.WriteByte(0);
                fs.WriteByte(0);
                fs.WriteByte(0);

                fs.WriteByte(0x36); // 4 bytes - offset of bitmap data
                fs.WriteByte(4); // should be 0x436
                fs.WriteByte(0);
                fs.WriteByte(0);

                fs.WriteByte(0x28); // 4 bytes - info header size
                fs.WriteByte(0); // 0x0e + 0x28 = 0x36, the palette
                fs.WriteByte(0);
                fs.WriteByte(0);

                fs.WriteByte((byte)image_width2); // 4 bytes image width
                fs.WriteByte((byte)(image_width2 >> 8));
                fs.WriteByte(0);
                fs.WriteByte(0);
                fs.WriteByte((byte)image_height2); // 4 bytes image height
                fs.WriteByte((byte)(image_height2 >> 8));
                fs.WriteByte(0);
                fs.WriteByte(0);

                fs.WriteByte(1); // 2 bytes number of planes
                fs.WriteByte(0);
                fs.WriteByte(8); // 2 bytes number of bits per pixel
                fs.WriteByte(0); //    8 bit = 256 color, indexed
                fs.WriteByte(0); // 4 bytes, compression = none
                fs.WriteByte(0);
                fs.WriteByte(0);
                fs.WriteByte(0);

                // 4 bytes image size (width * height)
                fs.WriteByte((byte)img_size); // image size, can be zero, if no compression
                fs.WriteByte((byte)(img_size >> 8));
                fs.WriteByte((byte)(img_size >> 16));
                fs.WriteByte(0);

                fs.WriteByte(0x12); // 4 byte - x pixels per meter
                fs.WriteByte(0x0b);
                fs.WriteByte(0);
                fs.WriteByte(0);
                fs.WriteByte(0x12); // 4 byte - y pixels per meter
                fs.WriteByte(0x0b);
                fs.WriteByte(0);
                fs.WriteByte(0);

                fs.WriteByte(0); // 4 byte - # colors in palette
                fs.WriteByte(1); // expected to be 0x100 for 8 bit
                fs.WriteByte(0);
                fs.WriteByte(0);

                fs.WriteByte(0); // 4 byte - # important colors in palette
                fs.WriteByte(0); // 0 is acceptable
                fs.WriteByte(0); 
                fs.WriteByte(0);

                //offset 0x36 --- 4 color palette, then all zero
                // R,G,B,0
                fs.WriteByte((byte)color1.B);
                fs.WriteByte((byte)color1.G);
                fs.WriteByte((byte)color1.R);
                fs.WriteByte(0);

                fs.WriteByte((byte)color2.B);
                fs.WriteByte((byte)color2.G);
                fs.WriteByte((byte)color2.R);
                fs.WriteByte(0);

                fs.WriteByte((byte)color3.B);
                fs.WriteByte((byte)color3.G);
                fs.WriteByte((byte)color3.R);
                fs.WriteByte(0);

                fs.WriteByte((byte)color4.B);
                fs.WriteByte((byte)color4.G);
                fs.WriteByte((byte)color4.R);
                fs.WriteByte(0);

                for(int i = 0; i < 252; i++)
                {
                    fs.WriteByte(0); // pad zero for the rest of the palette
                    fs.WriteByte(0);
                    fs.WriteByte(0);
                    fs.WriteByte(0);
                }

                // offset 0x436

                for (int yy = image_height2 - 1; yy >= 0; yy--) // reverse order
                {
                    for(int xx = 0; xx < image_width2; xx++)
                    {
                        fs.WriteByte((byte)Out_Array[(yy * 256) + xx]);
                    }
                }

                fs.Close();
            }

            label3.Focus();
        }

        private void checkBox3_Click(object sender, EventArgs e)
        { // round down to nearest 8
            if (has_loaded == 0)
            {
                label3.Focus();
                return;
            }

            if (checkBox3.Checked == true)
            {
                image_width = image_width & 0xfff8;
                image_height = image_height & 0xfff8;
            }
            else
            {
                image_width = remember_width;
                image_height = remember_height;
            }

            blank_left_BMP();
            revert_image();

            label3.Focus();
        }

        private void textBox2_Leave(object sender, EventArgs e)
        {
            u_width_set();
            label3.Focus();
        }

        private void textBox3_Leave(object sender, EventArgs e)
        {
            u_height_set();
            label3.Focus();
        }

        private void label3_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        { // check if Ctrl+V is pressed
            // lots of label3.Focus() to direct button presses here
            // prehaps some of them are unneeded ?

            if (e.KeyCode == Keys.V)
            {
                label3.Focus();

                paste_clipboard();
                
            }
        }


        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            reconvert();
            
            label3.Focus();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("2021 Doug Fraker.\nnesdoug.com");
            label3.Focus();
        }

        private void checkBox1_Click(object sender, EventArgs e)
        {
            label3.Focus();
        }

        private void exportImageToolStripMenuItem_Click(object sender, EventArgs e)
        { // save the left pic
            if (has_loaded == 0)
            {
                MessageBox.Show("No image loaded.");
                label3.Focus();
                return;
            }
            if (has_converted == 0)
            {
                MessageBox.Show("Image hasn't converted yet.");
                label3.Focus();
                return;
            }

            // save the left_bmp
            Rectangle cloneRect = new Rectangle(0, 0, image_width, image_height);
            System.Drawing.Imaging.PixelFormat format = conv_bmp.PixelFormat;
            Bitmap cloneBMP = conv_bmp.Clone(cloneRect, format);


            // open dialogue
            // save file
            // export image pic of the current view
            SaveFileDialog sfd = new SaveFileDialog();

            sfd.Filter = "PNG|*.png|BMP|*.bmp|JPG|*.jpg|GIF|*.gif";

            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string ext = System.IO.Path.GetExtension(sfd.FileName);
                switch (ext)
                {
                    case ".jpg":
                    case ".jpeg":
                        cloneBMP.Save(sfd.FileName, ImageFormat.Jpeg);
                        break;
                    case ".bmp":
                        cloneBMP.Save(sfd.FileName, ImageFormat.Bmp);
                        break;
                    case ".gif":
                        cloneBMP.Save(sfd.FileName, ImageFormat.Gif);
                        break;
                    default:
                        cloneBMP.Save(sfd.FileName, ImageFormat.Png);
                        break;

                }
            }
            label3.Focus();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        { // click on the picture, get the color -> selected color
            if (has_loaded == 0)
            {
                label3.Focus();
                return;
            }
            var mouseEventArgs = e as MouseEventArgs;
            if (mouseEventArgs == null)
            {
                label3.Focus();
                return;
            }

            int map_x = mouseEventArgs.X;
            int map_y = mouseEventArgs.Y;

            if ((map_x < 0) || (map_x >= image_width) ||
                (map_y < 0) || (map_y >= image_height) ) 
            {
                label3.Focus();
                return;
            }

            Color tempcolor = left_bmp.GetPixel(map_x, map_y);

            tempcolor = ToNES(tempcolor, -1);
            // ToNES also sets remember_index

            sel_color = tempcolor;
            pictureBox7.BackColor = sel_color;
            sel_color_val = remember_index;
            /*label14.Text = sel_color.R.ToString() + ", " +
                sel_color.G.ToString() + ", " +
                sel_color.B.ToString();*/

            string nes_str = GetNesVal(remember_index);
            label13.Text = nes_str;

            label3.Focus();
        }


        public string GetNesVal(int index)
        {
            if (index < 0) return "error 1";
            if (index > 51) return "error 2";
            if (index == 0) return "$0f";
            if (index == 1) return "$01";
            if (index == 2) return "$02";
            if (index == 3) return "$03";
            if (index == 4) return "$04";
            if (index == 5) return "$05";
            if (index == 6) return "$06";
            if (index == 7) return "$07";
            if (index == 8) return "$08";
            if (index == 9) return "$09";
            if (index == 10) return "$0a";
            if (index == 11) return "$0b";
            if (index == 12) return "$0c";
            if (index == 13) return "$00";
            if (index == 14) return "$11";
            if (index == 15) return "$12";
            if (index == 16) return "$13";
            if (index == 17) return "$14";
            if (index == 18) return "$15";
            if (index == 19) return "$16";
            if (index == 20) return "$17";
            if (index == 21) return "$18";
            if (index == 22) return "$19";
            if (index == 23) return "$1a";
            if (index == 24) return "$1b";
            if (index == 25) return "$1c";
            if (index == 26) return "$10";
            if (index == 27) return "$21";
            if (index == 28) return "$22";
            if (index == 29) return "$23";
            if (index == 30) return "$24";
            if (index == 31) return "$25";
            if (index == 32) return "$26";
            if (index == 33) return "$27";
            if (index == 34) return "$28";
            if (index == 35) return "$29";
            if (index == 36) return "$2a";
            if (index == 37) return "$2b";
            if (index == 38) return "$2c";
            if (index == 39) return "$30";
            if (index == 40) return "$31";
            if (index == 41) return "$32";
            if (index == 42) return "$33";
            if (index == 43) return "$34";
            if (index == 44) return "$35";
            if (index == 45) return "$36";
            if (index == 46) return "$37";
            if (index == 47) return "$38";
            if (index == 48) return "$39";
            if (index == 49) return "$3a";
            if (index == 50) return "$3b";
            if (index == 51) return "$3c";
            return "error 3";
        }

        public Color ToNES(Color tempcolor, int forbid)
        {
            // forbid is optional index to not allow.
            forbid = forbid * 3;
            // return closest color in the NES palette
            // 13 x 4 = 52 colors x 3 = 156 byte array
            int color_diff = 0, best_index = 0, lowest_diff = 999999;
            int dR = 0, dG = 0, dB = 0;
            int rr = 0;
            int gg = 1;
            int bb = 2;
            // check every NES color, which is closest match?
            for ( ; rr < 156; rr += 3, gg += 3, bb += 3)
            {
                if (rr == forbid) continue;
                dR = tempcolor.R - NES_PALETTE[rr];
                dG = tempcolor.G - NES_PALETTE[gg];
                dB = tempcolor.B - NES_PALETTE[bb];

                // note, the formula is supposed to take the Math.Sqrt()
                // of this but that step has been removed as unneeded.
                color_diff = ((dR * dR) + (dG * dG) + (dB * dB));

                if (color_diff < lowest_diff)
                {
                    lowest_diff = color_diff;
                    best_index = rr;
                }
            }

            rr = best_index;
            gg = best_index + 1;
            bb = best_index + 2;

            tempcolor = Color.FromArgb(NES_PALETTE[rr], NES_PALETTE[gg], NES_PALETTE[bb]);

            remember_index = best_index/3; // pass to global

            return tempcolor;
        }

        private void importImageToolStripMenuItem_Click(object sender, EventArgs e)
        { // load import an image 256x256 max
            
            // open dialogue, load image file

            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Open Image";
                dlg.Filter = "Image Files .png .jpg .bmp .gif)|*.png;*.jpg;*.bmp;*.gif|" + "All Files (*.*)|*.*";


                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    Bitmap temp_bmp = new Bitmap(dlg.FileName);

                    if ((temp_bmp.Height < 8) || (temp_bmp.Width < 8))
                    {
                        MessageBox.Show("Error. File too small?");
                        dlg.Dispose();
                        return;
                    }

                    has_loaded = 1;
                    has_converted = 0;

                    bright_adj = 0;
                    contrast_adj = 0;
                    textBox4.Text = "0";
                    textBox5.Text = "0";

                    float ratio1 = 1.0F;
                    float ratio2 = 1.0F;
                    int resize_width = user_width;//MAX_WIDTH;
                    int resize_height = user_height;//MAX_HEIGHT;
                    int need_resize = 0;

                    // clear it
                    for(int y = 0; y < MAX_HEIGHT; y++)
                    {
                        for (int x = 0; x < MAX_WIDTH; x++)
                        {
                            work_bmp.SetPixel(x, y, Color.Black);
                        }
                    }

                    // replaced MAX_WIDTH with user_width
                    if (temp_bmp.Width > user_width)
                    {
                        image_width = user_width;
                        ratio1 = temp_bmp.Width / (float)user_width;
                        need_resize = 1;
                    }
                    else
                    {
                        image_width = temp_bmp.Width;
                    }
                    // replaced MAX_HEIGHT with user_height
                    if (temp_bmp.Height > user_height)
                    {
                        image_height = user_height;
                        ratio2 = temp_bmp.Height / (float)user_height;
                        need_resize = 1;
                    }
                    else
                    {
                        image_height = temp_bmp.Height;
                    }

                    if((checkBox1.Checked == true) && (need_resize == 1))
                    {
                        // which is bigger? divide by that
                        if (ratio1 > ratio2)
                        {
                            resize_width = (int)Math.Round(temp_bmp.Width / ratio1);
                            resize_height = (int)Math.Round(temp_bmp.Height / ratio1);
                        }
                        else
                        {
                            resize_width = (int)Math.Round(temp_bmp.Width / ratio2);
                            resize_height = (int)Math.Round(temp_bmp.Height / ratio2);
                        }

                        using (Graphics g2 = Graphics.FromImage(work_bmp))
                        {
                            g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g2.DrawImage(temp_bmp, 0, 0, resize_width, resize_height);
                        }

                        image_width = resize_width;
                        image_height = resize_height;
                    }
                    else
                    {
                        // copy the bitmap, crop but don't resize
                        Rectangle copyRect = new Rectangle(0, 0, image_width, image_height);
                        using (Graphics g2 = Graphics.FromImage(work_bmp))
                        {
                            g2.DrawImage(temp_bmp, copyRect, copyRect, GraphicsUnit.Pixel);
                        }

                    }
                    

                    // copy again
                    Rectangle copyRect2 = new Rectangle(0, 0, image_width, image_height);
                    using (Graphics g2 = Graphics.FromImage(revert_bmp))
                    {
                        g2.DrawImage(work_bmp, copyRect2, copyRect2, GraphicsUnit.Pixel);
                    }

                    //Color temp_color = Color.Black;

                    // round down to nearest 8 ?
                    remember_width = image_width;
                    remember_height = image_height;
                    if (checkBox3.Checked == true)
                    {
                        image_width = image_width & 0xfff8;
                        image_height = image_height & 0xfff8;
                    }

                    blank_left_BMP();

                    Adjust_BMP();

                    Copy_2_Picbox();

                    // show the width and height
                    label4.Text = image_width.ToString();
                    label26.Text = image_height.ToString();

                    label6.Text = "Loaded";
                    label21.Text = "?";
                    label22.Text = "?";
                    temp_bmp.Dispose();
                }
                // it was locking up files, so...
                dlg.Dispose();
                GC.Collect();
            }
            label3.Focus();
        }

        
        


    }
}

