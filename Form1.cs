using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NESIFIER
{
    public partial class Form1 : Form
    {
        
        // sometimes auto generate outputs duplicates
        // todo, fix this issue



        public const int FLOYD_STEIN = 0;
        public const int BAYER8 = 1;
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

        public const int BAYER_MULT = 64;
        public static int dither_factor = 0;
        public static int dither_adjust = 0;
        public static double dither_db = 0.0;

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
        
        public static int[] Out_Indexes = new int[4] { 0, 1, 2, 3 };
        public static Color sel_color = Color.Black;
        public static Color color1 = Color.Black;
        public static Color color2 = Color.Black;
        public static Color color3 = Color.Black;
        public static Color color4 = Color.Black;
        public static int color1val, color2val, color3val, color4val, sel_color_val;
        public static int has_loaded = 0;
        public static int has_converted = 0;

        public static Bitmap orig_bmp = new Bitmap(256, 256);
        public static Bitmap conv_bmp = new Bitmap(256, 256);
        public static Bitmap left_bmp = new Bitmap(256, 256);
        public static Bitmap right_bmp = new Bitmap(256, 256); // dither scratchpad

        const int MAX_WIDTH = 256;
        const int MAX_HEIGHT = 256;
        public static int image_width, image_height;
        public static int remember_index;
        public static int[] Out_Array = new int[65536]; // for CHR output
        public static int[] CHR_16bytes = new int[16];

        // for auto color generator
        public static int[] R_Array = new int[65536];
        public static int[] G_Array = new int[65536];
        public static int[] B_Array = new int[65536];
        public static int[] Count_Array = new int[65536]; // count each color
        public static int color_count; // how many total different colors
        public static int r_val, g_val, b_val, diff_val;
        public static int c_offset, c_offset2;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = 0;
            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            label3.Focus();
            this.ActiveControl = label3;
        }


        private void pictureBox2_Click(object sender, EventArgs e)
        { // the NES palette box
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
            label14.Text = sel_color.R.ToString() + ", " +
                sel_color.G.ToString() + ", " +
                sel_color.B.ToString();

            label3.Focus();
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        { // color 1
            color1val = sel_color_val;
            color1 = sel_color;
            pictureBox3.BackColor = color1;
            label9.Text = label13.Text;

            label15.Text = color1.R.ToString() + ", " +
                color1.G.ToString() + ", " +
                color1.B.ToString();

            label3.Focus();
        }

        private void pictureBox4_Click(object sender, EventArgs e)
        { // color 2
            color2val = sel_color_val;
            color2 = sel_color;
            pictureBox4.BackColor = color2;
            label10.Text = label13.Text;

            label16.Text = color2.R.ToString() + ", " +
                color2.G.ToString() + ", " +
                color2.B.ToString();

            label3.Focus();
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        { // color 3
            color3val = sel_color_val;
            color3 = sel_color;
            pictureBox5.BackColor = color3;
            label11.Text = label13.Text;

            label17.Text = color3.R.ToString() + ", " +
                color3.G.ToString() + ", " +
                color3.B.ToString();

            label3.Focus();
        }

        private void pictureBox6_Click(object sender, EventArgs e)
        { // color 4
            color4val = sel_color_val;
            color4 = sel_color;
            pictureBox6.BackColor = color4;
            label12.Text = label13.Text;

            label18.Text = color4.R.ToString() + ", " +
                color4.G.ToString() + ", " +
                color4.B.ToString();

            label3.Focus();
        }

        private void button1_Click(object sender, EventArgs e)
        { // convert button
            if (has_loaded == 0)
            {
                label3.Focus();
                return;
            }

            has_converted = 1;

            label6.Text = "Converted";

            int red, green, blue, bayer_val;
            int red_dif, green_dif, blue_dif;
            
            // blank the out array (CHR indexes)
            for(int i = 0; i < 65536; i++)
            {
                Out_Array[i] = 0;
            }

            // convert the orig_bmp to conv_bmp, copy to picturebox

            for (int xx = 0; xx < MAX_WIDTH; xx++)
            {
                for (int yy = 0; yy < MAX_HEIGHT; yy++)
                {
                    conv_bmp.SetPixel(xx, yy, Color.Gray);
                }
            }

            Color tempcolor = Color.Black;
            Color tempcolor2 = Color.Black;
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
                        tempcolor = orig_bmp.GetPixel(xx, yy);
                        right_bmp.SetPixel(xx, yy, tempcolor);
                    }
                }
            }
            else // BAYER8
            {
                // do the dither now
                for (int yy = 0; yy < image_height; yy++) 
                {
                    for (int xx = 0; xx < image_width; xx++)
                    {
                        if(dither_factor > 0)
                        {
                            tempcolor = orig_bmp.GetPixel(xx, yy);
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
                            tempcolor = orig_bmp.GetPixel(xx, yy);
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

            label3.Focus();
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
            int closest_index = 0;
            int closest_val = 999;

            int test_val = Math.Abs(tempcolor.R - color1.R);
            test_val += Math.Abs(tempcolor.G - color1.G);
            test_val += Math.Abs(tempcolor.B - color1.B);
            if (test_val < closest_val)
            {
                closest_val = test_val;
                //closest_index = 0; // it is already
            }

            test_val = Math.Abs(tempcolor.R - color2.R);
            test_val += Math.Abs(tempcolor.G - color2.G);
            test_val += Math.Abs(tempcolor.B - color2.B);
            if (test_val < closest_val)
            {
                closest_val = test_val;
                closest_index = 1;
            }

            test_val = Math.Abs(tempcolor.R - color3.R);
            test_val += Math.Abs(tempcolor.G - color3.G);
            test_val += Math.Abs(tempcolor.B - color3.B);
            if (test_val < closest_val)
            {
                closest_val = test_val;
                closest_index = 2;
            }

            test_val = Math.Abs(tempcolor.R - color4.R);
            test_val += Math.Abs(tempcolor.G - color4.G);
            test_val += Math.Abs(tempcolor.B - color4.B);
            if (test_val < closest_val)
            {
                closest_val = test_val;
                closest_index = 3;
            }

            remember_index = closest_index;

            if (closest_index == 0) return color1;
            if (closest_index == 1) return color2;
            if (closest_index == 2) return color3;
            return color4;
        }


        private void saveAsCHRToolStripMenuItem_Click(object sender, EventArgs e)
        { // save CHR file
            if (has_loaded == 0)
            {
                MessageBox.Show("Image hasn't loaded yet.");
                return;
            }
            if (has_converted == 0)
            {
                MessageBox.Show("Image hasn't converted yet.");
                return;
            }

            // Out_Array[65536] y*256 + x

            // divide image into 128x128 segments
            // left to right, top to bottom in 8x8 chunks
            // top pixels, divide index into 2 - 1 bit things
            // roll all the lower bits (0-7) and upper bits (8-15)
            // into 16 total bytes per 8x8 tile

            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "bin File (*.chr)|*.chr|All files (*.*)|*.*";
            saveFileDialog1.Title = "Save the CHR";
            saveFileDialog1.ShowDialog();
            
            if (saveFileDialog1.FileName != "")
            {
                System.IO.FileStream fs = (System.IO.FileStream)saveFileDialog1.OpenFile();

                // do each 128x128 block separately
                // always do the top left 128x128
                c_offset2 = 0;
                for (int y = 0; y < 128; y += 8)
                {
                    for(int x = 0; x < 128; x += 8)
                    {
                        // process each 8x8 tile separately
                        Dry_CHR_Loop(x, y);

                        // have all 16 bytes, save them to file
                        for(int i = 0; i < 16; i++)
                        {
                            fs.WriteByte((byte)CHR_16bytes[i]);
                        }
                    }
                }
                // top right
                if(image_width > 127)
                {
                    c_offset2 = 128;

                    for (int y = 0; y < 128; y += 8)
                    {
                        for (int x = 0; x < 128; x += 8)
                        {
                            // process each 8x8 tile separately
                            Dry_CHR_Loop(x, y);

                            // have all 16 bytes, save them to file
                            for (int i = 0; i < 16; i++)
                            {
                                fs.WriteByte((byte)CHR_16bytes[i]);
                            }
                        }
                    }
                }
                // bottom left
                if(image_height > 127)
                {
                    c_offset2 = 32768;

                    for (int y = 0; y < 128; y += 8)
                    {
                        for (int x = 0; x < 128; x += 8)
                        {
                            // process each 8x8 tile separately
                            Dry_CHR_Loop(x, y);

                            // have all 16 bytes, save them to file
                            for (int i = 0; i < 16; i++)
                            {
                                fs.WriteByte((byte)CHR_16bytes[i]);
                            }
                        }
                    }
                }
                // bottom right
                if((image_width > 127) && (image_height > 127))
                {
                    c_offset2 = 128+32768;

                    for (int y = 0; y < 128; y += 8)
                    {
                        for (int x = 0; x < 128; x += 8)
                        {
                            // process each 8x8 tile separately
                            Dry_CHR_Loop(x, y);

                            // have all 16 bytes, save them to file
                            for (int i = 0; i < 16; i++)
                            {
                                fs.WriteByte((byte)CHR_16bytes[i]);
                            }
                        }
                    }
                }

                fs.Close();
            }

        }

        public void Dry_CHR_Loop(int x, int y)
        { // common loop code
            //c_offset = CHR_Array offset
            //c_offset2 = 128x128 offset

            // Out_Array = new int[65536]; // for CHR output

            int index0, index8, temp_bits, bit1, bit2, reorder;
            index0 = 0;
            index8 = 8;
            bit1 = 0;
            bit2 = 0;
            for (int y2 = 0; y2 < 8; y2++)
            {
                for (int x2 = 0; x2 < 8; x2++)
                {
                    c_offset = ((y + y2) * 256) + x + x2 + c_offset2;
                    reorder = Out_Indexes[Out_Array[c_offset] ];
                    bit1 = reorder & 1;
                    bit2 = reorder & 2;
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
            openFileDialog1.Filter = "bin File (*.bin)|*.bin|All files (*.*)|*.*";

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

                    DRY_Palette(); // print numbers and color boxes
                }
                else
                {
                    MessageBox.Show("Error. Expected 12 byte file.");
                }

                fs.Close();
            }
        }

        private void importNESToolStripMenuItem_Click(object sender, EventArgs e)
        { // import 4 bytes, NES indexes
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Title = "Open palette NES";
            openFileDialog1.Filter = "bin File (*.bin)|*.bin|All files (*.*)|*.*";

            int val1, val2, val3, val4;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                System.IO.FileStream fs = (System.IO.FileStream)openFileDialog1.OpenFile();
                if (fs.Length == 4)
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
                    MessageBox.Show("Error. Expected 4 byte file.");
                }

                fs.Close();
            }
        }

        public int NEStoPaletteIndex(int inValue)
        {
            // I regret rearranging the palette colors.
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
            saveFileDialog1.Filter = "bin File (*.bin)|*.bin|All files (*.*)|*.*";
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

        }

        private void exportNESToolStripMenuItem_Click(object sender, EventArgs e)
        { // export 4 bytes, NES indexes
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "bin File (*.bin)|*.bin|All files (*.*)|*.*";
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
        }

        private void button2_Click(object sender, EventArgs e)
        { // revert
            if (has_loaded == 0)
            {
                label3.Focus();
                return;
            }

            has_converted = 0;
            label6.Text = "Loaded";

            pictureBox1.Image = left_bmp;
            pictureBox1.Refresh();

            label3.Focus();
        }

        private void button8_Click(object sender, EventArgs e)
        { // revert and force grayscale
            if (has_loaded == 0)
            {
                label3.Focus();
                return;
            }
            has_converted = 0;
            label6.Text = "Loaded";

            // blank the left
            for (int xx = 0; xx < MAX_WIDTH; xx++)
            {
                for (int yy = 0; yy < MAX_HEIGHT; yy++)
                {
                    left_bmp.SetPixel(xx, yy, Color.Gray);
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
                    tempcolor = orig_bmp.GetPixel(x, y);
                    redf = tempcolor.R;
                    greenf = tempcolor.G;
                    bluef = tempcolor.B;
                    total = (int)((0.3 * redf) + (0.59 * greenf) + (0.11 * bluef));
                    tempcolor = Color.FromArgb(total, total, total);
                    orig_bmp.SetPixel(x, y, tempcolor);
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

                has_loaded = 1;
                has_converted = 0;

                // blank the left
                for (int xx = 0; xx < MAX_WIDTH; xx++)
                {
                    for (int yy = 0; yy < MAX_HEIGHT; yy++)
                    {
                        left_bmp.SetPixel(xx, yy, Color.Gray);
                    }
                }

                if (temp_bmp.Width > MAX_WIDTH)
                {
                    image_width = MAX_WIDTH;

                }
                else
                {
                    image_width = temp_bmp.Width;
                }

                if (temp_bmp.Height > MAX_HEIGHT)
                {
                    image_height = MAX_HEIGHT;

                }
                else
                {
                    image_height = temp_bmp.Height;
                }

                // copy the bitmap
                Rectangle cloneRect = new Rectangle(0, 0, image_width, image_height);
                System.Drawing.Imaging.PixelFormat format = temp_bmp.PixelFormat;
                orig_bmp = temp_bmp.Clone(cloneRect, format);

                Color temp_color = Color.Black;

                // copy pixel by pixel
                for (int xx = 0; xx < image_width; xx++)
                {
                    for (int yy = 0; yy < image_height; yy++)
                    {
                        temp_color = temp_bmp.GetPixel(xx, yy);
                        left_bmp.SetPixel(xx, yy, temp_color);
                    }
                }


                // show in picture box
                pictureBox1.Image = left_bmp;
                pictureBox1.Refresh();

                // show the width and height
                label7.Text = orig_bmp.Width.ToString();
                label8.Text = orig_bmp.Height.ToString();

                label6.Text = "Loaded";

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
            int color_found = 0;
            int red = 0, blue = 0, green = 0;
            int temp_var, closest_cnt, added;

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
            for (int i = 0; i < 65536; i++)
            {
                R_Array[i] = 0;
                G_Array[i] = 0;
                B_Array[i] = 0;
                Count_Array[i] = 0;
            }
            color_count = 0;

            Color tempcolor = Color.Black;

            // read all possible colors from the orig image
            // removing duplicates, keep track of how many
            for (int yy = 0; yy < image_height; yy++)
            {
                for (int xx = 0; xx < image_width; xx++)
                {
                    tempcolor = orig_bmp.GetPixel(xx, yy);
                    // speed it up, narrow the possibilities.
                    red = tempcolor.R & 0xf8;
                    blue = tempcolor.G & 0xf8;
                    green = tempcolor.B & 0xf8;
                    tempcolor = Color.FromArgb(red, blue, green);

                    // compare to all other colors, add if not present
                    if (color_count == 0)
                    {
                        Add_Color(tempcolor);
                        continue;
                    }

                    color_found = 0;
                    for (int i = 0; i < color_count; i++)
                    {
                        if((tempcolor.R == R_Array[i] &&
                            tempcolor.G == G_Array[i] &&
                            tempcolor.B == B_Array[i]))
                        { // color match found
                            Count_Array[i] = Count_Array[i] + 1;
                            color_found = 1;
                            break;
                        }
                    }
                    // no color match found
                    if(color_found == 0)
                    {
                        Add_Color(tempcolor);
                    }
                    
                }
            }
            label7.Text = color_count.ToString(); // print, how many colors

            // this mid point algorithm tends avoid extremes
            // give extra weight to the lowest value and the highest value
            // first find the darkest and lightest colors
            int darkest = 999;
            int darkest_index = 0;
            int lightest = 0;
            int lightest_index = 0;
            for (int i = 0; i < color_count; i++)
            {
                added = R_Array[i] + G_Array[i] + B_Array[i];
                if (added < darkest)
                {
                    darkest = added;
                    darkest_index = i;
                }
                if (added > lightest)
                {
                    lightest = added;
                    lightest_index = i;
                }
            }
            // give more count to them
            temp_var = image_width * image_height / 8; // 8 is magic
            Count_Array[darkest_index] += temp_var;
            Count_Array[lightest_index] += temp_var;

            // then reduce to 4 colors, using a mid point merge with
            // the closest neighbor color

            int color_count2 = color_count;
            while (color_count2 > 4)
            {
                //find the least count
                int least_index = 0;
                int least_cnt = 99999;
                for(int i = 0; i < color_count; i++)
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
                int closest_val = 999;
                r_val = R_Array[least_index];
                g_val = G_Array[least_index];
                b_val = B_Array[least_index];

                // find the closest to that one
                for (int i = 0; i < color_count; i++)
                {
                    if (Count_Array[i] == 0) continue;
                    diff_val = Math.Abs(r_val - R_Array[i]);
                    diff_val += Math.Abs(g_val - G_Array[i]);
                    diff_val += Math.Abs(b_val - B_Array[i]);
                    if (diff_val < closest_val)
                    {
                        closest_val = diff_val;
                        closest_index = i;
                    }
                }
                closest_cnt = Count_Array[closest_index];

                // merge closet index with least index, mid point
                temp_var = (closest_cnt + least_cnt);
                // the algorithm was (color1 + color2) / 2
                // but now, multiplied each by their count, div by both counts
                r_val = (R_Array[least_index] * least_cnt) + (R_Array[closest_index] * closest_cnt);
                r_val = (int)Math.Round((double)r_val/temp_var);
                g_val = (G_Array[least_index] * least_cnt) + (G_Array[closest_index] * closest_cnt);
                g_val = (int)Math.Round((double)g_val / temp_var);
                b_val = (B_Array[least_index] * least_cnt) + (B_Array[closest_index] * closest_cnt);
                b_val = (int)Math.Round((double)b_val / temp_var);
                R_Array[closest_index] = r_val;
                G_Array[closest_index] = g_val;
                B_Array[closest_index] = b_val;
                Count_Array[closest_index] = closest_cnt + least_cnt;

                color_count2--;
                
            }
            label8.Text = color_count2.ToString(); // print, final # colors (4)

            // find the 4 colors
            int find_color = 0;
            for(int i = 0; i < color_count; i++)
            {
                if (Count_Array[i] != 0)
                {
                    find_color = i;
                    break;
                }
            }
            r_val = R_Array[find_color];
            g_val = G_Array[find_color];
            b_val = B_Array[find_color];
            color1 = Color.FromArgb(r_val, g_val, b_val);

            if(color_count > 1)
            {
                for (int i = find_color+1; i < color_count; i++)
                {
                    if (Count_Array[i] != 0)
                    {
                        find_color = i;
                        break;
                    }
                }
                r_val = R_Array[find_color];
                g_val = G_Array[find_color];
                b_val = B_Array[find_color];
                color2 = Color.FromArgb(r_val, g_val, b_val);
            }

            if (color_count > 2)
            {
                for (int i = find_color + 1; i < color_count; i++)
                {
                    if (Count_Array[i] != 0)
                    {
                        find_color = i;
                        break;
                    }
                }
                r_val = R_Array[find_color];
                g_val = G_Array[find_color];
                b_val = B_Array[find_color];
                color3 = Color.FromArgb(r_val, g_val, b_val);
            }

            if (color_count > 3)
            {
                for (int i = find_color + 1; i < color_count; i++)
                {
                    if (Count_Array[i] != 0)
                    {
                        find_color = i;
                        break;
                    }
                }
                r_val = R_Array[find_color];
                g_val = G_Array[find_color];
                b_val = B_Array[find_color];
                color4 = Color.FromArgb(r_val, g_val, b_val);
            }

            DRY_Palette(); // don't repeat yourself
            label3.Focus();
        }

        public void DRY_Palette()
        {
            // convert all RGB to nearest NES color
            color1 = ToNES(color1); // ToNES also sets remember_index
            string nes_str = GetNesVal(remember_index);
            label9.Text = nes_str;
            color1val = remember_index;

            color2 = ToNES(color2);
            nes_str = GetNesVal(remember_index);
            label10.Text = nes_str;
            color2val = remember_index;

            color3 = ToNES(color3);
            nes_str = GetNesVal(remember_index);
            label11.Text = nes_str;
            color3val = remember_index;

            color4 = ToNES(color4);
            nes_str = GetNesVal(remember_index);
            label12.Text = nes_str;
            color4val = remember_index;

            // copy to the boxes
            pictureBox3.BackColor = color1;
            pictureBox4.BackColor = color2;
            pictureBox5.BackColor = color3;
            pictureBox6.BackColor = color4;

            // print the RGB
            label15.Text = color1.R.ToString() + ", " +
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
                color4.B.ToString();
        }


        public void Add_Color(Color tempcolor)
        {
            R_Array[color_count] = tempcolor.R;
            G_Array[color_count] = tempcolor.G;
            B_Array[color_count] = tempcolor.B;
            Count_Array[color_count] = 1;

            color_count++;
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        { // dither factor
            if (e.KeyChar == (char)Keys.Return)
            {
                dither_factor_set();

                e.Handled = true; // prevent ding on return press

                label3.Focus();
            }
        }

        private void textBox1_Leave(object sender, EventArgs e)
        { // dither factor
            dither_factor_set();
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
        }


        private void label3_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        { // check if Ctrl+V is pressed
            if (e.KeyCode == Keys.V)
            {
                paste_clipboard();
            }
        }


        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            label3.Focus();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("2021 Doug Fraker.\nnesdoug.com");
        }

        private void exportImageToolStripMenuItem_Click(object sender, EventArgs e)
        { // save the left pic
            if (has_loaded == 0)
            {
                MessageBox.Show("No image loaded.");
                return;
            }
            if (has_converted == 0)
            {
                MessageBox.Show("Image hasn't converted yet."); 
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
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        { // click on the picture
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

            tempcolor = ToNES(tempcolor);
            // ToNES also sets remember_index

            sel_color = tempcolor;
            pictureBox7.BackColor = sel_color;
            sel_color_val = remember_index;
            label14.Text = sel_color.R.ToString() + ", " +
                sel_color.G.ToString() + ", " +
                sel_color.B.ToString();

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

        public Color ToNES(Color tempcolor)
        {
            //13 x 4 = 52 colors x 3 = 156
            int diff = 0, best_index = 0, lowest_diff = 9999;
            int rr = 0;
            int gg = 1;
            int bb = 2;
            for ( ; rr < 156; rr += 3, gg += 3, bb += 3)
            {
                diff = Math.Abs(tempcolor.R - NES_PALETTE[rr]);
                diff += Math.Abs(tempcolor.G - NES_PALETTE[gg]);
                diff += Math.Abs(tempcolor.B - NES_PALETTE[bb]);
                if(diff < lowest_diff)
                {
                    lowest_diff = diff;
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
                    has_loaded = 1;
                    has_converted = 0;

                    // blank the left
                    for (int xx = 0; xx < MAX_WIDTH; xx++)
                    {
                        for (int yy = 0; yy < MAX_HEIGHT; yy++)
                        {
                            left_bmp.SetPixel(xx, yy, Color.Gray);
                        }
                    }

                    Bitmap temp_bmp = new Bitmap(dlg.FileName);

                    if (temp_bmp.Width > MAX_WIDTH)
                    {
                        image_width = MAX_WIDTH;
                        
                    }
                    else
                    {
                        image_width = temp_bmp.Width;
                    }

                    if (temp_bmp.Height > MAX_HEIGHT)
                    {
                        image_height = MAX_HEIGHT;

                    }
                    else
                    {
                        image_height = temp_bmp.Height;
                    }

                    // copy the bitmap
                    Rectangle cloneRect = new Rectangle(0, 0, image_width, image_height);
                    System.Drawing.Imaging.PixelFormat format = temp_bmp.PixelFormat;
                    orig_bmp = temp_bmp.Clone(cloneRect, format);

                    Color temp_color = Color.Black;

                    // copy pixel by pixel
                    for (int xx = 0; xx < image_width; xx++)
                    {
                        for (int yy = 0; yy < image_height; yy++)
                        {
                            temp_color = temp_bmp.GetPixel(xx, yy);
                            left_bmp.SetPixel(xx, yy, temp_color);
                        }
                    }


                    // show in picture box
                    pictureBox1.Image = left_bmp;
                    pictureBox1.Refresh();

                    // show the width and height
                    label7.Text = orig_bmp.Width.ToString();
                    label8.Text = orig_bmp.Height.ToString();

                    label6.Text = "Loaded";
                }

            }

        }

        
        private void button4_Click(object sender, EventArgs e)
        {
            Out_Indexes[0] += 1;
            Out_Indexes[0] &= 3;
            button4.Text = Out_Indexes[0].ToString();

            label3.Focus();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Out_Indexes[1] += 1;
            Out_Indexes[1] &= 3;
            button5.Text = Out_Indexes[1].ToString();

            label3.Focus();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            Out_Indexes[2] += 1;
            Out_Indexes[2] &= 3;
            button6.Text = Out_Indexes[2].ToString();

            label3.Focus();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            Out_Indexes[3] += 1;
            Out_Indexes[3] &= 3;
            button7.Text = Out_Indexes[3].ToString();

            label3.Focus();
        }


    }
}

