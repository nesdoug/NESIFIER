// DZ4 compression by Doug Fraker 2021


// compresses an array, output to rle_array



using System.Windows.Forms;

namespace NESIFIER
{
    public partial class Form1
    {

        public static byte[] orig_array = new byte[65536];
        public static byte[] rle_array = new byte[70000]; // extra big, no errors
        public static byte[] mark_lit = new byte[65536];
        public static int orig_size, rle_size, percent;

        public static int orig_index, rle_index, rle_count;
        public static int pattern_size;
        public static int ref_addr, ref_len;
        public static int hacky_index, hacky_count;
        public static int ref_used, ref_bytes;



        public int CompressIt(int[] in_arr, int arr_size)
        {
            // take the orig_array and compress it with dz4, to rle_array
            orig_index = 0;
            rle_index = 0;
            rle_count = 0;
            ref_used = 0;
            ref_bytes = 0;

            orig_size = arr_size;

            if (orig_size < 1)
            {
                MessageBox.Show("Error. File too small.");
                return 0;
            }

            for (int i = 0; i < 65536; i++)
            {
                mark_lit[i] = 0;
            }

            for (int i = 0; i < orig_size; i++)
            {
                orig_array[i] = (byte)in_arr[i];
            }


            if (orig_size < 3)
            {
                // possible errors with extra small files...
                // just do a literal of 1 or 2 bytes
                rle_array[0] = (byte)(orig_size - 1);
                for (int i = 0; i < orig_size; i++)
                {
                    rle_array[i + 1] = orig_array[i];
                }
                rle_array[orig_size + 1] = (byte)(0xff);
                rle_size = orig_size + 2;
            }
            else
            {
                // now compress it
                while (orig_index < orig_size)
                {
                    //
                    if (orig_index < orig_size - 1)
                    {
                        if (orig_array[orig_index] == orig_array[orig_index + 1])
                        {
                            mark_lit[orig_index] = 1; // RLE
                        }
                        else if (FindPattern() == 1) // RLE 2 byte
                        {

                            // RLE 2 byte = 4
                            mark_lit[orig_index] = (byte)(2 + pattern_size); // 2 + 2 = 4

                        }
                        else if (orig_array[orig_index] == orig_array[orig_index + 1] - 1)
                        {
                            mark_lit[orig_index] = 2; // RLE +
                        }
                        //wrap around ff to 00
                        else if ((orig_array[orig_index] == 0xff) &&
                                (orig_array[orig_index + 1] == 0))
                        {
                            mark_lit[orig_index] = 2;
                        }
                        else
                        {
                            mark_lit[orig_index] = 3; // literal
                        }
                    }
                    else
                    {
                        //last one always mark 3
                        mark_lit[orig_index] = 3; // literal
                    }

                    orig_index += 1;
                }


                // remove small runs of 1
                for (int i = 1; i < orig_size - 1; i++)
                {
                    if (i >= 65535) break;
                    if (mark_lit[i] >= 4) continue; // pattern
                    if (mark_lit[i] == 1)
                    {
                        if (orig_array[i] == 0) continue;
                        if (orig_array[i] == 0xff) continue;
                    }

                    if ((mark_lit[i] != mark_lit[i - 1]) &&
                        (mark_lit[i] != mark_lit[i + 1]))
                    {
                        // all runs of 1 = literal
                        mark_lit[i] = 3;
                    }
                }


                // parse it and copy as needed
                // rle_index = 0;
                for (int i = 0; i < orig_size; i++)
                {
                    if (mark_lit[i] == 0) break; // nothing should be marked 0
                    if (mark_lit[i] == 1) // RLE
                    {
                        Count_RLE(i);
                        if (rle_count < 32)
                        { // use a 1 byte header
                            if (orig_array[i] == 0) // special
                            {
                                rle_array[rle_index] = (byte)(0x80 + (rle_count & 0x1f));
                                rle_index++;
                                // only needs 1 byte
                            }
                            else if (orig_array[i] == 0xff) // special
                            {
                                rle_array[rle_index] = (byte)(0xa0 + (rle_count & 0x1f));
                                rle_index++;
                                // only needs 1 byte
                            }
                            else
                            {
                                rle_array[rle_index] = (byte)(0x20 + (rle_count & 0x1f));
                                rle_index++;
                                rle_array[rle_index] = orig_array[i];
                                rle_index++;
                            }

                        }
                        else // long
                        { // 2 byte header
                            if (orig_array[i] == 0) // special
                            {
                                rle_array[rle_index] = (byte)(0xf0 + ((rle_count >> 8) & 0x03));
                                rle_index++;
                                rle_array[rle_index] = (byte)(rle_count);
                                rle_index++;
                                // no value byte
                            }
                            else if (orig_array[i] == 0xff) // special
                            {
                                rle_array[rle_index] = (byte)(0xf4 + ((rle_count >> 8) & 0x03));
                                rle_index++;
                                rle_array[rle_index] = (byte)(rle_count);
                                rle_index++;
                                // no value byte
                            }
                            else
                            {
                                rle_array[rle_index] = (byte)(0xe4 + ((rle_count >> 8) & 0x03));
                                rle_index++;
                                rle_array[rle_index] = (byte)(rle_count);
                                rle_index++;
                                rle_array[rle_index] = orig_array[i];
                                rle_index++;
                            }

                        }
                        i += rle_count;
                    }
                    else if (mark_lit[i] == 2) // RLE +1 sequential numbers
                    {
                        Count_RLEP(i);
                        if (rle_count < 32)
                        { // use a 1 byte header
                            rle_array[rle_index] = (byte)(0x40 + (rle_count & 0x1f));
                            rle_index++;
                            rle_array[rle_index] = orig_array[i];
                            rle_index++;
                        }
                        else
                        { // 2 byte header
                            rle_array[rle_index] = (byte)(0xe8 + ((rle_count >> 8) & 0x03));
                            rle_index++;
                            rle_array[rle_index] = (byte)(rle_count);
                            rle_index++;
                            rle_array[rle_index] = orig_array[i];
                            rle_index++;
                        }
                        i += rle_count;
                    }
                    else if (mark_lit[i] >= 4) // RLE multi byte pattern of 2
                    {
                        Count_RLED(i);

                        if (rle_count < 32)
                        { // always 1 byte header
                            rle_array[rle_index] = (byte)(0x60 + (rle_count & 0x1f));
                            rle_index++;

                        }
                        else // long
                        {
                            rle_array[rle_index] = (byte)(0xec + ((rle_count >> 8) & 0x03));
                            rle_index++;
                            rle_array[rle_index] = (byte)(rle_count);
                            rle_index++;

                        }

                        for (int j = 0; j < pattern_size; j++)
                        {
                            rle_array[rle_index] = orig_array[i + j];
                            rle_index++;
                        }
                        rle_count++;
                        rle_count *= pattern_size;
                        rle_count--;

                        i += rle_count;
                    }
                    else
                    { // literal = 3 (or back reference)
                        Count_Literal(i);  // returns rle_count
                        hacky_index = -2; // just an invalid, to be ignored in Back_Reference()
                        // test for back references.
                        if (Back_Reference(i) == 1)
                        {
                            //ref_addr, ref_len; globals, should be exact already
                            rle_array[rle_index] = (byte)(0xc0 + (ref_len & 0x1f));
                            rle_index++;
                            rle_array[rle_index] = (byte)(ref_addr);
                            rle_index++;
                            i += ref_len;

                        }
                        else
                        {
                            hacky_index = rle_index;
                            if (rle_count < 32)
                            { // use a 1 byte header 00
                                rle_array[rle_index] = (byte)(rle_count & 0x1f);
                                rle_index++;

                            }
                            else
                            { // 2 byte header c0
                                rle_array[rle_index] = (byte)(0xe0 + ((rle_count >> 8) & 0x03));
                                rle_index++;
                                rle_array[rle_index] = (byte)(rle_count & 0xff);
                                rle_index++;

                            }
                            hacky_count = 0;
                            // copy the literal string now
                            for (int j = 0; j < rle_count + 1; j++)
                            {
                                rle_array[rle_index] = orig_array[i];
                                rle_index++;
                                // hacky solution, back references in the middle of literal
                                // every byte added, test AGAIN for back references.
                                i++;
                                hacky_count++;
                                if ((hacky_count < rle_count) &&
                                   (Back_Reference(i) == 1))
                                { // back ref found
                                    // fix the literal header
                                    if (rle_count < 32)
                                    {
                                        rle_array[hacky_index] = (byte)(j & 0x1f);
                                    }
                                    else
                                    {
                                        rle_array[hacky_index] = (byte)(0xe0 + ((j >> 8) & 0x03));
                                        rle_array[hacky_index + 1] = (byte)(j & 0xff);
                                    }

                                    // insert back ref stuff
                                    rle_array[rle_index] = (byte)(0xc0 + (ref_len & 0x1f));
                                    rle_index++;
                                    rle_array[rle_index] = (byte)(ref_addr);
                                    rle_index++;
                                    i += ref_len; // ok
                                    i++; //back to original -1 plan
                                    break;
                                }

                            }
                            i--; // oops, too many.

                        }

                    }

                }

                // put a final marker 0xff
                rle_array[rle_index] = (byte)0xff;

                rle_size = rle_index + 1;
            }



            //has_compressed = true;
            string str = orig_size.ToString();
            str = str + " original bytes\n";
            str = str + rle_size.ToString();
            str = str + " dz4 bytes\n";
            //label3.Text = str;

            //if (rle_size > 65536) rle_size = 65536;
            percent = rle_size * 100 / orig_size;
            if (percent < 0) percent = 0;
            if (percent > 100000) percent = 0;

            str = str + percent.ToString();
            str = str + " %";
            if (percent == 0) str = "< 1%";
            //label4.Text = str;
            MessageBox.Show(str);

            return 1; // success
        }



        public int Back_Reference(int index)
        { // there is no output buffer, so we can only use rle_array
            int best_index = 0;
            int best_size = 0;
            int test_index = 0;
            int test_size = 0;
            int test_index2 = 0;
            int test_index3 = 0;

            int start_val = 255 - index; // 255
            if (start_val < 0) start_val = 0;
            if (start_val >= 252) return 0; // 252

            // the error was a back reference to the header byte I adjust
            // see below the bug fix.

            for (int i = start_val; i < 253; i++) // 253
            {
                test_index = rle_index + i - 255; // 255
                if (test_index < 0) continue;
                test_size = 0;
                for (int j = 0; j < 32; j++)
                {
                    test_index2 = index + j;// forward in the orig array
                    if (test_index2 > orig_size - 1) break;
                    test_index3 = test_index + j; // back in rle array
                    if (test_index3 >= rle_index) break;
                    if (test_index3 == hacky_index) break; // bug fix, skip header
                    if ((test_index3 == hacky_index + 1) && (rle_count > 0x1f))
                    { // 2 byte header bug fix
                        break;
                    }
                    if (orig_array[test_index2] == rle_array[test_index3])
                    {
                        test_size++;
                    }
                    else break;
                }
                if (test_size > best_size)
                {
                    best_size = test_size;
                    best_index = test_index;
                }
            }
            if (best_size > 3)
            {
                // ref_addr, ref_len
                ref_addr = (best_index - rle_index) - 1;
                ref_addr &= 0xff;
                ref_len = best_size - 1; // changed back to original -1 plan
                return 1;
            }

            return 0;
        }


        public int FindPattern()
        { // global orig_index has the position
            int test_index, test_index2;

            // this was a test of a range of values, now just 2
            // the code could be rewritten

            for (int i = 2; i < 3; i++) // just 2
            {
                int pattern_found = 0;
                for (int j = i - 1; j >= 0; j--)
                {
                    test_index = j + orig_index;
                    test_index2 = j + i + orig_index;
                    if (test_index > orig_size - 1) break;
                    if (test_index2 > orig_size - 1) break;
                    if (orig_array[test_index] == orig_array[test_index2]) pattern_found++;
                }
                if (pattern_found == i)
                {
                    pattern_size = i;
                    return 1;
                }
            }

            return 0;
        }


        public void Count_RLE(int index)
        { // simple one byte RLE, value 1
            rle_count = 1;
            index++;
            if (index > orig_size - 1) return;
            while (mark_lit[index] == 1)
            {
                rle_count++;
                index++;
                if (index > orig_size - 1) break;
            }
            if (rle_count > 0x3ff) rle_count = 0x3ff;
        }


        public void Count_RLEP(int index)
        { // each value is +1 previous, value 2
            rle_count = 1;
            index++;
            if (index > orig_size - 1) return;
            while (mark_lit[index] == 2)
            {
                rle_count++;
                index++;
                if (index > orig_size - 1) break;
            }
            if (rle_count > 0x3ff) rle_count = 0x3ff;
        }

        public void Count_RLED(int index)
        { // find a multi byte pattern
            // count only the same number
            int key_mark = mark_lit[index];
            pattern_size = key_mark - 2;

            rle_count = 1;
            index += pattern_size;
            if (index > orig_size - 1) return;
            while (mark_lit[index] == key_mark)
            {
                rle_count++;
                index += pattern_size;
                if (index > orig_size - 1) break;
            }

            // 1 byte header 1-256
            if (rle_count > 0x3ff) rle_count = 0x3ff;
        }


        public void Count_Literal(int index)
        { // literal string of bytes, value 4
            rle_count = 0;
            index++;
            if (index > orig_size - 1) return;
            while (mark_lit[index] == 3)
            {
                rle_count++;
                index++;
                if (index > orig_size - 1) break;
            }
            if (rle_count > 0x3ff) rle_count = 0x3ff;
        }



    }
}