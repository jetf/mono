using gboolean = System.Boolean;
using gint = System.Int32;
using gpointer = System.IntPtr;
using guint32 = System.UInt32;
using guint16 = System.UInt16;

using GdkColormapPointer = System.IntPtr;
using GdkDrawablePointer = System.IntPtr;
using GdkGCPointer = System.IntPtr;
using GdkNativeWindowPointer = System.IntPtr;
using GdkPixbufPointer = System.IntPtr;
using GdkPixmapPointer = System.IntPtr;
using GdkRectanglePointer = System.IntPtr;
using GdkWindowPointer = System.IntPtr;
using GtkStylePointer = System.IntPtr;
using GtkWidgetPointer = System.IntPtr;

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace System.Windows.Forms.VisualStyles
{
	/// <summary>
	/// </summary>
	/// <remarks>
	/// Inspired by ThemeGtk and QGtkStyle (http://labs.trolltech.com/page/Projects/Styles/GtkStyle).
	/// </remarks>
	class GtkPlus
	{
		static GtkPlus instance;
		public static GtkPlus Instance {
			get { return instance; }
		}
		public static bool Initialize ()
		{
			//TODO: If we need to pass the actual arguments, duplicate the required code from GTK#.
			int argc = 0;
			string [] argv = new string [1];
			bool result;
			try {
				result = gtk_init_check (ref argc, ref argv);
			} catch (DllNotFoundException) {
				return false;
			}
			if (result)
				instance = new GtkPlus ();
			return result;
		}

		readonly int WidgetTypeCount = Enum.GetNames (typeof (WidgetType)).Length;
		GtkWidgetPointer [] widgets;
		GtkStylePointer [] styles;
		IStylePainter [] style_painters;
		GtkStateType state_type;
		GtkShadowType shadow_type;
		string detail;
		protected GtkPlus ()
		{
			widgets = new GtkWidgetPointer [WidgetTypeCount];
			styles = new GtkStylePointer [WidgetTypeCount];
			style_painters = new IStylePainter [WidgetTypeCount];
			#region Widget types
			#region Button
			g_object_ref_sink (widgets [(int)WidgetType.Button] = gtk_button_new ());
			style_painters [(int)WidgetType.Button] = new ButtonPainter ();
			#endregion
			#region CheckBox
			g_object_ref_sink (widgets [(int)WidgetType.CheckBox] = gtk_check_button_new ());
			style_painters [(int)WidgetType.CheckBox] = new CheckBoxPainter ();
			#endregion
			#endregion
			for (int widget_index = 0; widget_index < WidgetTypeCount; widget_index++)
				g_object_ref (styles [widget_index] = gtk_rc_get_style (widgets [widget_index]));
		}
		~GtkPlus ()
		{
			for (int widget_index = 0; widget_index < WidgetTypeCount; widget_index++) {
				g_object_unref (widgets [widget_index]);
				g_object_unref (styles [widget_index]);
			}
		}
		#region Controls
		public void PaintCheckBox (IDeviceContext dc, Rectangle rectangle)
		{
			state_type = GtkStateType.GTK_STATE_NORMAL;
			shadow_type = GtkShadowType.GTK_SHADOW_ETCHED_OUT;
			detail = null;
			Paint (WidgetType.CheckBox, rectangle, dc);
		}
		#endregion
		void Paint (WidgetType widget_type, Rectangle rectangle, IDeviceContext dc)
		{
			Paint (widget_type, rectangle, dc, TransparencyType.Alpha, Color.Black, DeviceContextType.Native);
			//Paint (widget_type, rectangle, dc, TransparencyType.None, Color.Black, DeviceContextType.Graphics);
		}
		void Paint (WidgetType widget_type, Rectangle rectangle, IDeviceContext dc, TransparencyType transparencyType, Color background, DeviceContextType deviceContextType)
		{
			GdkDrawablePointer drawable = gdk_pixmap_new (IntPtr.Zero, rectangle.Width, rectangle.Height, 24);
			GtkStylePointer style = styles [(int)widget_type] = gtk_style_attach (styles [(int)widget_type], drawable);
			GdkPixbufPointer pixbuf;
			IntPtr pixel_data;
			int rowstride;
			GdkGCPointer gc = gdk_gc_new (drawable);
			GdkColor color = new GdkColor (background);
			gdk_gc_set_rgb_fg_color (gc, ref color);
			Paint (drawable, gc, rectangle, widget_type, out pixbuf, out pixel_data, out rowstride);
			GdkPixbufPointer white_pixbuf = IntPtr.Zero;
			IntPtr white_pixel_data = IntPtr.Zero;
			int white_rowstride = 0;
			GdkColor white_color = new GdkColor();
			if (transparencyType == TransparencyType.Alpha) {
				white_color.red = guint16.MaxValue;
				white_color.green = guint16.MaxValue;
				white_color.blue = guint16.MaxValue;
				gdk_gc_set_rgb_fg_color (gc, ref white_color);
				Paint (drawable, gc, rectangle, widget_type, out white_pixbuf, out white_pixel_data, out white_rowstride);
			}
			g_object_unref (gc);
			unsafe {
				byte* row = (byte*)pixel_data;
				byte* pixel;
				byte* white_row = (byte*)white_pixel_data;
				byte* white_pixel;

				for (int row_index = 0; row_index < rectangle.Height; row_index++) {
					pixel = row;
					white_pixel = white_row;
					for (int pixel_index = 0; pixel_index < rectangle.Width; pixel_index++) {
						const int GdkRedOffset = 0;
						const int GdkGreenOffset = 1;
						const int GdkBlueOffset = 2;
						const int BitmapAlphaOffset = 3;
						const int BitmapRedOffset = 2;
						const int BitmapBlueOffset = 0;
						switch (transparencyType) {
						case TransparencyType.Alpha:
							pixel [BitmapAlphaOffset] = (byte)(pixel [GdkRedOffset] - white_pixel [GdkRedOffset] + byte.MaxValue);
							break;
						case TransparencyType.Color:
							if (
								pixel [GdkRedOffset] == background.R &&
								pixel [GdkGreenOffset] == background.G &&
								pixel [GdkBlueOffset] == background.B) {
								const int AlphaFullTransparent = 0;
								pixel [BitmapAlphaOffset] = AlphaFullTransparent;
							}
							break;
						}

						byte temporary = pixel [GdkRedOffset];
						pixel [BitmapBlueOffset] = pixel [GdkBlueOffset];
						pixel [BitmapRedOffset] = temporary;

						const int PixelSize = 4;
						pixel += PixelSize;
						white_pixel += PixelSize;
					}
					row += rowstride;
					white_row += white_rowstride;
				}
			}
			if (transparencyType == TransparencyType.Alpha)
				g_object_unref (white_pixbuf);
			g_object_unref (drawable);
			Bitmap bitmap = new Bitmap (rectangle.Width, rectangle.Height, rowstride, PixelFormat.Format32bppPArgb, pixel_data);
			Graphics g;
            bool graphics_is_from_hdc = false;
            switch (deviceContextType) {
            case DeviceContextType.Graphics:
                g = (Graphics)dc;
                break;
            case DeviceContextType.Native:
                g = Graphics.FromHdc (dc.GetHdc ());
                break;
            default:
#if NET_2_0
                g = dc as Graphics;
                if (g == null) {
                    graphics_is_from_hdc = true;
                    g = Graphics.FromHdc (dc.GetHdc ());
                } else
                    graphics_is_from_hdc = false;
#else
				g = (Graphics)dc;
#endif
                break;
            }
			g.DrawImage (bitmap, rectangle.Location);
            switch (deviceContextType) {
            case DeviceContextType.Graphics:
                break;
            case DeviceContextType.Native:
                g.Dispose ();
                dc.ReleaseHdc ();
                break;
            default:
#if NET_2_0
                if (graphics_is_from_hdc) {
                    g.Dispose ();
                    dc.ReleaseHdc ();
                }
#endif
                break;
            }
			bitmap.Dispose ();
			g_object_unref (pixbuf);
		}
		void Paint (GdkDrawablePointer drawable, GdkGCPointer gc, Rectangle rectangle, WidgetType widget_type, out GdkPixbufPointer pixbuf, out IntPtr pixel_data, out int rowstride)
		{
			gdk_draw_rectangle (drawable, gc, true, 0, 0, rectangle.Width, rectangle.Height);
			style_painters [(int)widget_type].Paint (styles [(int)widget_type], drawable, state_type, shadow_type, IntPtr.Zero, widgets [(int)widget_type], detail, 0, 0, rectangle.Width, rectangle.Height);
			if (
				(pixbuf = gdk_pixbuf_new (GdkColorspace.GDK_COLORSPACE_RGB, true, 8, rectangle.Width, rectangle.Height)) == IntPtr.Zero ||
				gdk_pixbuf_get_from_drawable (pixbuf, drawable, IntPtr.Zero, 0, 0, 0, 0, rectangle.Width, rectangle.Height) == IntPtr.Zero)
				throw new OutOfMemoryException ();
			pixel_data = gdk_pixbuf_get_pixels (pixbuf);
			rowstride = gdk_pixbuf_get_rowstride (pixbuf);
		}
		interface IStylePainter
		{
			void Paint (GtkStylePointer style, GdkWindowPointer window, GtkStateType state_type, GtkShadowType shadow_type, GdkRectanglePointer area, GtkWidgetPointer widget, string detail, gint x, gint y, gint width, gint height);
		}
		#region Style painters
		class ButtonPainter : IStylePainter
		{
			public void Paint (GtkStylePointer style, GdkWindowPointer window, GtkStateType state_type, GtkShadowType shadow_type, GdkRectanglePointer area, GtkWidgetPointer widget, string detail, gint x, gint y, gint width, gint height)
			{
				gtk_paint_box (style, window, state_type, shadow_type, area, widget, detail, x, y, width, height);
			}
		}
		class CheckBoxPainter : IStylePainter
		{
			public void Paint (GtkStylePointer style, GdkWindowPointer window, GtkStateType state_type, GtkShadowType shadow_type, GdkRectanglePointer area, GtkWidgetPointer widget, string detail, gint x, gint y, gint width, gint height)
			{
				gtk_paint_check (style, window, state_type, shadow_type, area, widget, detail, x, y, width,height);
			}
		}
		#endregion
		enum WidgetType
		{
			Button,
			CheckBox
		}
		enum TransparencyType
		{
			None,
			Color,
			Alpha
		}
		enum DeviceContextType
		{
			Unknown,
			Graphics,
			Native
		}
		#region Platform Invoke
#if VISUAL_STYLES_USE_GTKPLUS_ON_WINDOWS
		const string GobjectLibraryName = "libgobject-2.0-0.dll";
		const string GdkLibraryName = "libgdk-win32-2.0-0.dll";
		const string GdkPixbufLibraryName = "libgdk_pixbuf-2.0-0.dll";
		const string GtkLibraryName = "libgtk-win32-2.0-0.dll";
#else
		const string GobjectLibraryName = "libgobject-2.0.so";
		const string GdkLibraryName = "libgdk-x11-2.0.so";
		const string GdkPixbufLibraryName = "libgdk_pixbuf-2.0.so";
		const string GtkLibraryName = "libgtk-x11-2.0.so";
#endif
		#region GDK
		[DllImport (GdkLibraryName)]
		static extern void gdk_draw_rectangle (GdkDrawablePointer drawable, GdkGCPointer gc, gboolean filled, gint x, gint y, gint width, gint height);
		[DllImport (GdkLibraryName)]
		static extern GdkGCPointer gdk_gc_new (GdkDrawablePointer drawable);
		[DllImport (GdkLibraryName)]
		static extern void gdk_gc_set_rgb_fg_color (GdkGCPointer gc, ref GdkColor color);
		[DllImport (GdkLibraryName)]
		static extern GdkPixbufPointer gdk_pixbuf_get_from_drawable (GdkPixbufPointer dest, GdkDrawablePointer src, GdkColormapPointer cmap, int src_x, int src_y, int dest_x, int dest_y, int width, int height);
		[DllImport (GdkLibraryName)]
		static extern GdkPixmapPointer gdk_pixmap_new (GdkDrawablePointer drawable, gint width, gint height, gint depth);
		struct GdkColor{
			public guint32 pixel;
			public guint16 red;
			public guint16 green;
			public guint16 blue;
			public GdkColor (Color value)
			{
				pixel = 0;
				red = (guint16)(value.R << 8);
				green = (guint16)(value.G << 8);
				blue = (guint16)(value.B << 8);
			}
		}
		#endregion
		#region GdkPixbuf
		[DllImport (GdkPixbufLibraryName)]
		static extern IntPtr gdk_pixbuf_get_pixels (GdkPixbufPointer pixbuf);
		[DllImport (GdkPixbufLibraryName)]
		static extern int gdk_pixbuf_get_rowstride (GdkPixbufPointer pixbuf);
		[DllImport (GdkPixbufLibraryName)]
		static extern GdkPixbufPointer gdk_pixbuf_new (GdkColorspace colorspace, gboolean has_alpha, int bits_per_sample, int width, int height);
		[DllImport (GdkPixbufLibraryName)]
		static extern GdkPixbufPointer gdk_pixbuf_new_from_data (IntPtr data, GdkColorspace colorspace, gboolean has_alpha, int bits_per_sample, int width, int height, int rowstride, IntPtr destroy_fn, gpointer destroy_fn_data);
		enum GdkColorspace{
			GDK_COLORSPACE_RGB
		}
		#endregion
		#region GTK
		[DllImport (GtkLibraryName)]
		static extern gboolean gtk_init_check (ref int argc, ref string[] argv);
		[DllImport (GtkLibraryName)]
		static extern GtkStylePointer gtk_rc_get_style (GtkWidgetPointer widget);
		[DllImport (GtkLibraryName)]
		static extern GtkStylePointer gtk_style_attach (GtkStylePointer style, GdkWindowPointer window);
		#region Widget creation
		[DllImport (GtkLibraryName)]
		static extern GtkWidgetPointer gtk_button_new ();
		[DllImport (GtkLibraryName)]
		static extern GtkWidgetPointer gtk_check_button_new ();
		#endregion
		#region Painting
		[DllImport (GtkLibraryName)]
		static extern void gtk_paint_box (GtkStylePointer style, GdkWindowPointer window, GtkStateType state_type, GtkShadowType shadow_type, GdkRectanglePointer area, GtkWidgetPointer widget, string detail, gint x, gint y, gint width, gint height);
		[DllImport (GtkLibraryName)]
		static extern void gtk_paint_check (GtkStylePointer style, GdkWindowPointer window, GtkStateType state_type, GtkShadowType shadow_type, GdkRectanglePointer area, GtkWidgetPointer widget, string detail, gint x, gint y, gint width, gint height);
		#endregion
		enum GtkShadowType
		{
			GTK_SHADOW_NONE,
			GTK_SHADOW_IN,
			GTK_SHADOW_OUT,
			GTK_SHADOW_ETCHED_IN,
			GTK_SHADOW_ETCHED_OUT
		}
		enum GtkStateType
		{
			GTK_STATE_NORMAL,
			GTK_STATE_ACTIVE,
			GTK_STATE_PRELIGHT,
			GTK_STATE_SELECTED,
			GTK_STATE_INSENSITIVE
		}
		#endregion
		#region GObject
		[DllImport (GobjectLibraryName)]
		static extern gpointer g_object_ref (gpointer @object);
		[DllImport (GobjectLibraryName)]
		static extern void g_object_unref (gpointer @object);
		[DllImport (GobjectLibraryName)]
		static extern gpointer g_object_ref_sink (gpointer @object);
		#endregion
		#endregion
	}
}
