/*
 File: TilingView.m
 Abstract: Handles tile drawing and tile image loading.
 Version: 1.1
 
 Disclaimer: IMPORTANT:  This Apple software is supplied to you by Apple
 Inc. ("Apple") in consideration of your agreement to the following
 terms, and your use, installation, modification or redistribution of
 this Apple software constitutes acceptance of these terms.  If you do
 not agree with these terms, please do not use, install, modify or
 redistribute this Apple software.
 
 In consideration of your agreement to abide by the following terms, and
 subject to these terms, Apple grants you a personal, non-exclusive
 license, under Apple's copyrights in this original Apple software (the
 "Apple Software"), to use, reproduce, modify and redistribute the Apple
 Software, with or without modifications, in source and/or binary forms;
 provided that if you redistribute the Apple Software in its entirety and
 without modifications, you must retain this notice and the following
 text and disclaimers in all such redistributions of the Apple Software.
 Neither the name, trademarks, service marks or logos of Apple Inc. may
 be used to endorse or promote products derived from the Apple Software
 without specific prior written permission from Apple.  Except as
 expressly stated in this notice, no other rights or licenses, express or
 implied, are granted by Apple herein, including but not limited to any
 patent rights that may be infringed by your derivative works or by other
 works in which the Apple Software may be incorporated.
 
 The Apple Software is provided by Apple on an "AS IS" basis.  APPLE
 MAKES NO WARRANTIES, EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION
 THE IMPLIED WARRANTIES OF NON-INFRINGEMENT, MERCHANTABILITY AND FITNESS
 FOR A PARTICULAR PURPOSE, REGARDING THE APPLE SOFTWARE OR ITS USE AND
 OPERATION ALONE OR IN COMBINATION WITH YOUR PRODUCTS.
 
 IN NO EVENT SHALL APPLE BE LIABLE FOR ANY SPECIAL, INDIRECT, INCIDENTAL
 OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 INTERRUPTION) ARISING IN ANY WAY OUT OF THE USE, REPRODUCTION,
 MODIFICATION AND/OR DISTRIBUTION OF THE APPLE SOFTWARE, HOWEVER CAUSED
 AND WHETHER UNDER THEORY OF CONTRACT, TORT (INCLUDING NEGLIGENCE),
 STRICT LIABILITY OR OTHERWISE, EVEN IF APPLE HAS BEEN ADVISED OF THE
 POSSIBILITY OF SUCH DAMAGE.
 
 Copyright (C) 2010 Apple Inc. All Rights Reserved.
 
 */

using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.CoreAnimation;
using MonoTouch.CoreGraphics;
using MonoTouch.UIKit;
using MonoTouch.ObjCRuntime;


namespace NPhotoViewController
{
	public class TilingView : UIView
	{
		/*
		Not really sure how to approach the problem of overriding the (Class)
		'static' in the Objective-C example
		-- i.e. what I tried was WRONG [CD] see below for the correct method
		private CATiledLayer __layer;
		public override CALayer Layer {
			get 
			{	// set in ctor
				return __layer;
			}
		}	*/
		
		/// <summary>
		/// Thanks to @migueldeicaza for the _correct_ pattern to use here
		/// </summary>
		[Export ("layerClass")]
		public static Class LayerClass ()
		{
			return new Class (typeof (CATiledLayer));
		}
		
		/// <summary>
		/// initWithImageName
		/// </summary>
		public TilingView (string name, SizeF size) : base(new RectangleF(0, 0, size.Width, size.Height))
		{
			imageName = name;

			var tiledLayer = (CATiledLayer)this.Layer;
			tiledLayer.LevelsOfDetail = 4;
		}
		
		public string imageName {get;set;}
		public bool annotates {get;set;}
		
		public override void Draw (RectangleF rect)
		{
			var context = UIGraphics.GetCurrentContext();
			
			// get the scale from the context by getting the current transform matrix, then asking for
		    // its "a" component, which is one of the two scale components. We could also ask for "d".
		    // This assumes (safely) that the view is being scaled equally in both dimensions.
			var scale = context.GetCTM().xx;  // .a			// http://developer.apple.com/library/ios/#documentation/GraphicsImaging/Reference/CGAffineTransform/Reference/reference.html#//apple_ref/doc/c_ref/CGAffineTransform
			
			var tiledLayer = (CATiledLayer)this.Layer;
			var tileSize = tiledLayer.TileSize;
			
			// Even at scales lower than 100%, we are drawing into a rect in the coordinate system of the full
		    // image. One tile at 50% covers the width (in original image coordinates) of two tiles at 100%. 
		    // So at 50% we need to stretch our tiles to double the width and height; at 25% we need to stretch 
		    // them to quadruple the width and height; and so on.
		    // (Note that this means that we are drawing very blurry images as the scale gets low. At 12.5%, 
		    // our lowest scale, we are stretching about 6 small tiles to fill the entire original image area. 
		    // But this is okay, because the big blurry image we're drawing here will be scaled way down before 
		    // it is displayed.)
		    tileSize.Width /= scale;
		    tileSize.Height /= scale;
			
			// calculate the rows and columns of tiles that intersect the rect we have been asked to draw
		    int firstCol = (int)Math.Floor(rect.GetMinX() / tileSize.Width);
		    int lastCol  = (int)Math.Floor( (rect.GetMaxX()-1) / tileSize.Width);
		    int firstRow = (int)Math.Floor(rect.GetMinY() / tileSize.Height);
		    int lastRow  = (int)Math.Floor( (rect.GetMaxY()-1) / tileSize.Height);
			
		    for (int row = firstRow; row <= lastRow; row++) 
			{
		        for (int col = firstCol; col <= lastCol; col++) 
				{
		            UIImage tile = tileForScale(scale, row, col);
		            RectangleF tileRect = new RectangleF(  tileSize.Width * col
					                                     , tileSize.Height * row
					                                     , tileSize.Width
					                                     , tileSize.Height);
		
		            // if the tile would stick outside of our bounds, we need to truncate it so as to avoid
		            // stretching out the partial tiles at the right and bottom edges
		            tileRect.Intersect (this.Bounds);
		
					tile.Draw(tileRect);
		            
		            if (this.annotates)
					{
						UIColor.White.SetColor();
		                context.SetLineWidth(6.0f / scale);
		                context.StrokeRect(tileRect);
		            }
		        }
		    }
		}
		 
		public UIImage tileForScale (Single scale, int row, int col)
		{
			// we use "imageWithContentsOfFile:" instead of "imageNamed:" here because we don't want UIImage to cache our tiles
			var tileName = String.Format("Images/ImageTiles/{0}_{1}_{2}_{3}", imageName, Convert.ToInt32(scale*1000), col, row);
			//var path = NSBundle.MainBundle.PathForResource(tileName, "png");
			var path = tileName + ".png"; 	// HACK: resource paths screwed up [CD]
			UIImage image = UIImage.FromFile(path);
			return image;
		}
	}
}