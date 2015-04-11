// dcraw.net - camera raw file decoder
// Copyright (C) 1997-2008  Dave Coffin, dcoffin a cybercom o net
// Copyright (C) 2008-2009  Sam Webster, Dave Brown
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.Collections.Generic;
using System.IO;
using dcraw.Demosaicing;
using dcraw.Filters;

namespace dcraw
{
    public class Settings
    {
        public int user_flip = -1;
        public bool timestamp_only = false;
        public bool identify_only = false;
        public bool thumbnail_only = false;
        public bool use_fuji_rotate = false;
        public int user_qual = -1;
        public int user_black = -1;
        public int user_sat = -1;

        public Settings(int user_flip, bool timestamp_only, bool identify_only, bool thumbnail_only, bool use_fuji_rotate, int user_qual, int user_black, int user_sat)
        {
            this.user_flip = user_flip;
            this.timestamp_only = timestamp_only;
            this.identify_only = identify_only;
            this.thumbnail_only = thumbnail_only;
            this.use_fuji_rotate = use_fuji_rotate;
            this.user_qual = user_qual;
            this.user_black = user_black;
            this.user_sat = user_sat;
        }
    }

    public class MainStuff
    {
        public static void DoStuff(string filename, DcRawState state, Settings settings)
        {
            try
            {
                DoStuff_internal(filename, state, settings);
            }
            finally
            {
                Profiler.DumpStats();
            }

            //ShowPicture(state);
        }

        public static void DoStuff_internal(string filename, DcRawState state, Settings settings)
        {
            state.inFilename = filename;

            state.ifp = new RawStream(filename);

            Identifier id = new Identifier(state);
            id.identify(state.ifp);

            // Works this far...

            if (settings.user_flip >= 0)
            {
                state.flip = settings.user_flip;
            }

            switch ((state.flip + 3600) % 360)
            {
                case 270:
                    state.flip = 5;
                    break;
                case 180:
                    state.flip = 3;
                    break;
                case 90:
                    state.flip = 6;
                    break;
            }

            if (settings.timestamp_only)
            {
                if (state.timestamp == null)
                {
                    Console.WriteLine("{0} has no timestamp.", filename);
                }
                else if (settings.identify_only)
                {
                    //"%10ld%10d %s"
                    Console.WriteLine("{0}{1} {2}", state.timestamp, state.shot_order, filename);
                }
                else
                {
                    if (state.verbose)
                    {
                        Console.WriteLine("{0} time set to {1}.\n", filename, state.timestamp);
                    }

                    //ut.actime = ut.modtime = state.timestamp;
                    //utime ((char*)state.ifname, &ut);
                }
                return;
            }

            // TODO: need writer
            Tiff t = new Tiff(state);
            state.write_fun = t.write_ppm_tiff;

            if (settings.thumbnail_only)
            {
                if (state.thumb_offset == 0)
                {
                    Console.WriteLine("{0} has no thumbnail.\n", filename);
                    return;
                }

                if (state.thumb_load_raw != null)
                {
                    state.load_raw = state.thumb_load_raw;
                    state.data_offset = state.thumb_offset;
                    state.height = state.thumb_height;
                    state.width = state.thumb_width;
                    state.filters = 0;
                }
                else
                {
                    state.ifp.Seek(state.thumb_offset, SeekOrigin.Begin);
                    //fseek (state.ifp, state.thumb_offset, SEEK_SET);
                    state.write_fun = state.write_thumb;
                    goto thumbnail;
                }
            }

            if (state.load_raw is kodak_ycbcr_load_raw)
            {
                state.height += state.height & 1;
                state.width += state.width & 1;
            }

            if (settings.identify_only && state.verbose && state.make != null)
            {
                Console.WriteLine("Filename: {0}", filename);
                //long localtimestamp = state.timestamp;
                //Console.WriteLine("Timestamp: %s", ctime(&localtimestamp));
                Console.WriteLine("Camera: {0} {1}", state.make, state.model);
                if (state.artist != null)
                {
                    Console.WriteLine("Owner: {0}", state.artist);
                }
                if (state.dng_version != 0)
                {
                    Console.Write("DNG Version: ");
                    for (int i = 24; i >= 0; i -= 8)
                    {
                        Console.WriteLine("{0}{1}", state.dng_version >> i & 255, i != 0 ? '.' : '\n');
                    }
                }
                Console.WriteLine("ISO speed: {0}", (int) state.iso_speed);
                Console.Write("Shutter: ");
                if (state.shutter > 0 && state.shutter < 1)
                {
                    Console.Write("1/");
                    state.shutter = 1/state.shutter;
                }
                Console.WriteLine("{0} sec", state.shutter);
                Console.WriteLine("Aperture: f/{0}", state.aperture);
                Console.WriteLine("Focal length: {0} mm", state.focal_len);
                Console.WriteLine("Embedded ICC profile: {0}", state.profile_length != 0 ? "yes" : "no");
                Console.WriteLine("Number of raw images: {0}", state.is_raw);
                if (state.pixel_aspect != 1)
                {
                    Console.WriteLine("Pixel Aspect Ratio: {0}", state.pixel_aspect);
                }
                if (state.thumb_offset != 0)
                {
                    Console.WriteLine("Thumb size:  {0} x {1}", state.thumb_width, state.thumb_height);
                }
                Console.WriteLine("Full size:   {0} x {1}", state.raw_width, state.raw_height);
            }
            else if (state.is_raw == 0)
            {
                Console.WriteLine("Cannot decode file {0}", filename);
            }

            if (state.is_raw == 0) return;

            state.shrink = (state.filters != 0 &&
                            (state.half_size || state.threshold != 0 || state.aber[0] != 1 || state.aber[2] != 1))
                               ? (ushort) 1
                               : (ushort) 0;
            state.iheight = (state.height + state.shrink) >> state.shrink;
            state.iwidth = (state.width + state.shrink) >> state.shrink;
            if (settings.identify_only)
            {
                if (state.verbose)
                {
                    if (settings.use_fuji_rotate)
                    {
                        if (state.fuji_width != 0)
                        {
                            state.fuji_width = (state.fuji_width - 1 + state.shrink) >> state.shrink;
                            state.iwidth = (int) (state.fuji_width/Math.Sqrt(0.5));
                            state.iheight = (int) ((state.iheight - state.fuji_width)/Math.Sqrt(0.5));
                        }
                        else
                        {
                            if (state.pixel_aspect < 1)
                            {
                                state.iheight = (int) (state.iheight/state.pixel_aspect + 0.5f);
                            }
                            if (state.pixel_aspect > 1)
                            {
                                state.iwidth = (int) (state.iwidth*state.pixel_aspect + 0.5f);
                            }
                        }
                    }
                    if ((state.flip & 4) != 0)
                    {
                        int temp = state.iheight;
                        state.iheight = state.iwidth;
                        state.iwidth = temp;
                    }
                    Console.WriteLine("Image size:  {0} x {1}", state.width, state.height);
                    Console.WriteLine("Output size: {0} x {1}", state.iwidth, state.iheight);
                    Console.WriteLine("Raw colors: {0}", state.colors);
                    if (state.filters != 0)
                    {
                        Console.WriteLine("\nFilter pattern: ");
                        //if (!state.cdesc[3]) state.cdesc[3] = 'G';
                        for (int i = 0; i < 16; i++)
                        {
                            //Console.Write(state.cdesc[state.fc(i >> 1, i & 1)]);
                        }
                    }
                    Console.Write("\nDaylight multipliers:");
                    for (int c = 0; c < state.colors; c++)
                    {
                        Console.Write(" {0}", state.pre_mul[c]);
                    }

                    if (state.cam_mul[0] > 0)
                    {
                        Console.Write("\nCamera multipliers:");
                        for (int c = 0; c < 4; c++)
                        {
                            Console.Write(" {0}", state.cam_mul[c]);
                        }
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("{0} is a {1} {2} image.\n", filename, state.make, state.model);
                }

                state.ifp.Close();
                return;
            }

            if (state.use_camera_matrix && state.cmatrix[0, 0] > 0.25)
            {
                Array.Copy(state.cmatrix, state.rgb_cam, state.cmatrix.Length);
            }

            //memcpy (rgb_cam, cmatrix, sizeof cmatrix);
            state.raw_color = false;

            state.image = new ushort[state.iheight * state.iwidth * 4];

            //SetImage((ushort (*)[4]) state.Alloc (state.iheight * state.iwidth * sizeof *IMAGE));

            // TODO: implement metadata support if we need foveon support
            /*
			if (state.meta_length != 0) {
			    AllocMetadata(state);
				//state.meta_data = (signed char *) state.Alloc (state.meta_length);
				//merror (state.meta_data, "main()");
			}
             */
			if (state.verbose)
				Console.WriteLine("Loading {0} {1} image from {2} ...", state.make, state.model, filename);
			if (state.shot_select >= state.is_raw)
			{
			    Console.WriteLine("{0}: \"-s {1}\" requests a nonexistent image!", filename, state.shot_select);
            }

            state.ifp.Seek(state.data_offset, SeekOrigin.Begin);

            using (Profiler.BlockProfile("Loader: " + state.load_raw.GetType()))
            {
                state.load_raw.LoadRaw();
            }

            //System::Console::WriteLine("load_raw()  -  {0}", load_raw.Method);

			//load_raw();

            
#if false
            // TODO
            if (state.zero_is_bad) {
                remove_zeroes();
            }
#endif

#if false
            // TODO
			bad_pixels (bpfile);
#endif

#if false
            // TODO
            if (dark_frame) {
                subtract (dark_frame);
            }
#endif

			int quality = 2 + (state.fuji_width == 0 ? 1 : 0);
            if (settings.user_qual >= 0)
            {
                quality = settings.user_qual;
            }

            if (settings.user_black >= 0)
            {
                state.black = (uint)settings.user_black;
            }

            if (settings.user_sat > 0)
            {
                state.maximum = (uint)settings.user_sat;
            }

//#ifdef COLORCHECK
			//colorcheck();
            //#endif


#if false
            // TODO
            if (state.is_foveon && !state.document_mode) {
                foveon_interpolate();
            }
#endif
            IList<Filter> filters = GetFilters(state, settings, quality);

            foreach(Filter f in filters)
            {
                using (Profiler.BlockProfile("Filter: " + f))
                {
                    f.Process();
                }
            }

        thumbnail:

            string write_ext;
            /*if (write_fun == gcnew WriteDelegate(&CLASS jpeg_thumb)) {
				write_ext = ".jpg";
            } else if (state.output_tiff && write_fun == gcnew WriteDelegate(&CLASS write_ppm_tiff)) {*/
				write_ext = ".tiff";
            /*} else {
				write_ext = (char*)".pgm\0.ppm\0.ppm\0.pam" + state.colors*5-5;
            }*/

            string ofname = state.inFilename;
            ofname = Path.ChangeExtension(ofname, write_ext);

            /*if (state.multi_out)
				sprintf (ofname+strlen(ofname), "_%0*d",
				snprintf(0,0,"%d",state.is_raw-1), state.shot_select);
			if (thumbnail_only) ofname += ".thumb";*/

		    Stream ofp = File.OpenWrite(ofname);
			if (state.verbose) Console.WriteLine("Writing data to {0} ...\n", ofname);
            using (Profiler.BlockProfile("Writer: " + state.write_fun.Method.ReflectedType + "." + state.write_fun.Method.Name))
            {
                state.write_fun(ofp);
            }

            state.ifp.Close();
			ofp.Close();
        }

        private static IList<Filter> GetFilters(DcRawState state, Settings settings, int quality)
        {
            IList<Filter> filters = new List<Filter>();

            if (!state.is_foveon && state.document_mode < 2) {
                //scale_colors();
                Filter colourScaler = new ColourScaler(state);
                filters.Add(colourScaler);
                //colourScaler.Process();
            }

            // Select demosaicing filter
            Filter demosaic = GetDemosaic(state, quality);
            filters.Add(demosaic);
            //demosaic.Process();

            if (state.mix_green) {
                Filter greenMixer = new GreenMixer(state);
                filters.Add(greenMixer);
                //greenMixer.Process();
            }

            if (!state.is_foveon && state.colors == 3) {
                //median_filter();
                Filter medianFilter = new Median(state);
                filters.Add(medianFilter);
                //medianFilter.Process();
            }

            if (!state.is_foveon && state.highlight == 2) {
                throw new NotImplementedException();
                //blend_highlights();
            }

            if (!state.is_foveon && state.highlight > 2) {
                throw new NotImplementedException();
                //recover_highlights();
            }

            if (settings.use_fuji_rotate && state.fuji_width != 0)
            {
                throw new NotImplementedException();
                //fuji_rotate();
            }

//#ifndef NO_LCMS
            //if (cam_profile) apply_profile (cam_profile, out_profile);
            //#endif

            Filter colourSpace = new ColourSpace(state);
            filters.Add(colourSpace);
            //colourSpace.Process();
            //convert_to_rgb();

            if (settings.use_fuji_rotate && state.pixel_aspect != 1)
            {
                throw new NotImplementedException();
                //stretch();
            }
            return filters;
        }

        private class GreenMixer : Filter
        {
            public GreenMixer(DcRawState state) : base(state) {}

            public override void Process()
            {
                state.colors = 3;
                for (int i = 0; i < state.height * state.width; i++)
                {
                    state.image[i * 4 + 1] = (ushort)(((state.image[i * 4 + 1] + state.image[i * 4 + 3])) >> 1);
                    //IMAGE[i][1] = (IMAGE[i][1] + IMAGE[i][3]) >> 1;
                }
            }
        }

        private static Filter GetDemosaic(DcRawState state, int quality)
        {
            Filter demosaic;
            if (state.filters != 0 && state.document_mode == 0) {
                if (quality == 0)
                {
                    demosaic = new Bilinear(state);
                }
                else if (quality == 1 || state.colors > 3)
                {
                    Demosaic.PreInterpolate(state);
                    throw new NotImplementedException();
                    //vng_interpolate();
                }
                else if (quality == 2)
                {
                    Demosaic.PreInterpolate(state);
                    throw new NotImplementedException();
                    //ppg_interpolate();
                }
                else
                {
                    demosaic = new AHD(state);
                }
            } else {
                demosaic = new BasicDemosiac(state);
            }

            return demosaic;
        }
    }
}
