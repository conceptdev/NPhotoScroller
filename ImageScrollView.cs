/*
 File: ImageScrollView.m
 Abstract: Centers image within the scroll view and configures image sizing and display.
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
	public class ImageScrollView : UIScrollView
	{
		UIView imageView;
		public int index {get;set;}
		
		public ImageScrollView (RectangleF frame) : base (frame)
		{
			ShowsVerticalScrollIndicator = false;
			ShowsHorizontalScrollIndicator = false;
			BouncesZoom = true;
			DecelerationRate = 0.990f; //UIScrollViewDecelerationRateFast;
			
			#region UIScrollView delegate methods
			ViewForZoomingInScrollView = 
				delegate (UIScrollView scrollView)
				{
					return imageView;
				};
			#endregion
			
		}
		
		#region Override layoutSubviews to center content
		public override void LayoutSubviews ()
		{
			base.LayoutSubviews ();
			
			// center the image as it becomes smaller than the size of the screen
			var boundsSize = this.Bounds.Size;
			var frameToCenter = imageView.Frame;
			
			// center horizontally
			if (frameToCenter.Size.Width < boundsSize.Width)
				frameToCenter.X = (boundsSize.Width - frameToCenter.Size.Width) / 2;
			else
				frameToCenter.X = 0;
			
			// center vertically
			if (frameToCenter.Size.Height < boundsSize.Height)
				frameToCenter.Y = (boundsSize.Height - frameToCenter.Size.Height) / 2;
			else
				frameToCenter.Y = 0;
			
			imageView.Frame = frameToCenter;
			
			if (imageView is TilingView)
			{
				// to handle the interaction between CATiledLayer and high resolution screens, we need to manually set the
		        // tiling view's contentScaleFactor to 1.0. (If we omitted this, it would be 2.0 on high resolution screens,
		        // which would cause the CATiledLayer to ask us for tiles of the wrong scales.)
				if (imageView.RespondsToSelector(new Selector("scale")))
					imageView.ContentScaleFactor = 1.0f;		// beware pre-iOS4 [CD]
			}
			
		}
		#endregion
		
		#region Configure scrollView to display new image (tiled or not)
		public void displayImage (UIImage image)
		{
			// clear the previous imageView
			if (imageView != null)
			{
				imageView.RemoveFromSuperview();
				imageView.Dispose();
				imageView = null; // Not sure we need this [CD]
			}
			// reset our zoomScale to 1.0 before doing any further calculations
			this.ZoomScale = 1.0f;
			
			// make a new UIImageView for the new image
			imageView = new UIImageView (image);
			this.AddSubview (imageView);
			
			this.ContentSize = image.Size;					Console.WriteLine("this.ContentSize " + image.Size);
			this.setMaxMinZoomScalesForCurrentBounds();
			this.ZoomScale = this.MinimumZoomScale;
			Console.WriteLine("this.MinimumZoomScale " + this.MinimumZoomScale);
			Console.WriteLine("this.ZoomScale " + this.ZoomScale);
		}
		
		public void displayTiledImageNamed (string imageName, SizeF imageSize)
		{
			// clear the previous imageView
			if (imageView != null)
			{
				imageView.RemoveFromSuperview();
				imageView.Dispose();
				imageView = null; // Not sure we need this [CD]
			}
			// reset our zoomScale to 1.0 before doing any further calculations
			this.ZoomScale = 1.0f;
			
			imageView = new TilingView (imageName, imageSize);
			((TilingView)imageView).annotates = true; 				// ** remove this line to remove the white tile grid **
			this.AddSubview(imageView);
			
			this.ContentSize = imageSize;					Console.WriteLine("this.ContentSize " + imageSize);
			this.setMaxMinZoomScalesForCurrentBounds();
			this.ZoomScale = this.MinimumZoomScale;					// ** remove this line to see the image 'zoomed in' **
			Console.WriteLine("this.MinimumZoomScale " + this.MinimumZoomScale);
			Console.WriteLine("this.ZoomScale " + this.ZoomScale);
		}
		
		public void setMaxMinZoomScalesForCurrentBounds()
		{
			var boundsSize = this.Bounds.Size;
			var imageSize = imageView.Bounds.Size;
			
			float xScale = boundsSize.Width / imageSize.Width;    // the scale needed to perfectly fit the image width-wise
			float yScale = boundsSize.Height / imageSize.Height;  // the scale needed to perfectly fit the image height-wise
			float minScale = Math.Min(xScale, yScale);            // use minimum of these to allow the image to become fully visible
    
		    // on high resolution screens we have double the pixel density, so we will be seeing every pixel if we limit the
		    // maximum zoom scale to 0.5.
			float maxScale = 1.0f / UIScreen.MainScreen.Scale; // beware pre-iOS4 [CD]
			
			// don't let minScale exceed maxScale. (If the image is smaller than the screen, we don't want to force it to be zoomed.) 
		    if (minScale > maxScale)
			{
		        minScale = maxScale;
		    }
		    
		    this.MaximumZoomScale = maxScale;
		    this.MinimumZoomScale = minScale;
		}
		#endregion
		
		#region Methods called during rotation to preserve the zoomScale and the visible portion of the image
		// returns the center point, in image coordinate space, to try to restore after rotation. 
		public PointF pointToCenterAfterRotation()
		{
			var boundsCenter = new PointF(this.Bounds.GetMidX(), this.Bounds.GetMidY());
			return this.ConvertPointToView (boundsCenter, imageView);
		}
		
		// returns the zoom scale to attempt to restore after rotation. 
		public float scaleToRestoreAfterRotation ()
		{
			var contentScale = this.ZoomScale;
			
			// If we're at the minimum zoom scale, preserve that by returning 0, which will be converted to the minimum
		    // allowable scale when the scale is restored.
		    if (contentScale <= this.MinimumZoomScale + float.Epsilon)
		        contentScale = 0;
		    
		    return contentScale;
		}
		
		PointF maximumContentOffset 
		{
			get
			{
				var contentSize = this.ContentSize;
				var boundsSize = this.Bounds.Size;
				return new PointF(contentSize.Width - boundsSize.Width, contentSize.Height - boundsSize.Height);
			}
		}
		PointF minimumContentOffset
		{
			get
			{
				return new PointF(0,0); //PointF.Empty; // zero? [CD]
			}
		}
		
		// Adjusts content offset and scale to try to preserve the old zoomscale and center.
		public void restoreCenterPoint (PointF oldCenter, float oldScale)
		{
			 // Step 1: restore zoom scale, first making sure it is within the allowable range.
		    this.ZoomScale = Math.Min(this.MaximumZoomScale, Math.Max(this.MinimumZoomScale, oldScale));
		    
		    
		    // Step 2: restore center point, first making sure it is within the allowable range.
		    
		    // 2a: convert our desired center point back to our own coordinate space
		    var boundsCenter = this.ConvertPointFromView(oldCenter,imageView);
		    // 2b: calculate the content offset that would yield that center point
		    var offset = new PointF (boundsCenter.X - this.Bounds.Size.Width / 2.0f, 
		                                 boundsCenter.Y - this.Bounds.Size.Height / 2.0f);
		    // 2c: restore offset, adjusted to be within the allowable range
		    var maxOffset = this.maximumContentOffset;
		    var minOffset = this.minimumContentOffset;
		    offset.X = Math.Max(minOffset.X, Math.Min(maxOffset.X, offset.X));
		    offset.Y = Math.Max(minOffset.Y, Math.Min(maxOffset.Y, offset.Y));
		    this.ContentOffset = offset;
		}
		#endregion
		
		public override string ToString ()
		{
			return string.Format ("[ImageScrollView: index={0}, frame={1}]", index, Frame);
		}
	}
}

