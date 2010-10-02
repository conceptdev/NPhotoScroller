/*
 File: PhotoViewController.m
 Abstract: Configures and displays the paging scroll view and handles tiling and page configuration.
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
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Linq;

namespace NPhotoViewController
{
	public class PhotoViewController : UIViewController
	{
		/// <summary>
		/// true: use the CATiledLayer to show a 'multi-scale-image' which is memory-efficient
		/// false: load the full-size image into memory for scaling
		/// </summary>
		bool useTiledImage = true;
		
		UIScrollView pagingScrollView;
		
		HashSet<ImageScrollView> recycledPages;
		HashSet<ImageScrollView> visiblePages;
		
	    // these values are stored off before we start rotation so we adjust our content offset appropriately during rotation
    	int   firstVisiblePageIndexBeforeRotation;
    	float percentScrolledIntoFirstVisiblePage;
		
		public PhotoViewController ()
		{
		}
		
		#region View loading and unloading
		public override void LoadView ()
		{
			// Step 1: make the outer paging scroll view
			var pagingScrollViewFrame = this.frameForPagingScrollView;
			pagingScrollView = new UIScrollView (pagingScrollViewFrame);
			pagingScrollView.PagingEnabled = true;
			pagingScrollView.BackgroundColor = UIColor.Black;
			pagingScrollView.ShowsVerticalScrollIndicator = false;
			pagingScrollView.ShowsHorizontalScrollIndicator = false;
			pagingScrollView.ContentSize = this.contentSizeForPagingScrollView;
			
			#region ScrollView delegate methods
			pagingScrollView.Scrolled += 
			delegate 
			{
				this.tilePages();
			};
			#endregion
			
			this.View = pagingScrollView;
			
			// Step 2: prepare to tile content
			recycledPages = new HashSet<ImageScrollView>();
			visiblePages = new HashSet<ImageScrollView>();
			
			this.tilePages();
		}
		
		public override void ViewDidUnload ()
		{
			base.ViewDidUnload ();
			pagingScrollView.Dispose();
			pagingScrollView = null;
			recycledPages = null;
			visiblePages = null;
		}
		protected override void Dispose (bool disposing)
		{
			pagingScrollView.Dispose();
			base.Dispose (disposing);
		}
		#endregion
		
		#region Tiling and page configuration
		void tilePages ()
		{
			// Calculate which pages are visible
		    var visibleBounds = pagingScrollView.Bounds;
		    int firstNeededPageIndex = (int)Math.Floor(visibleBounds.GetMinX() / visibleBounds.Width);
		    int lastNeededPageIndex  = (int)Math.Floor((visibleBounds.GetMaxX()-1) / visibleBounds.Width);
		    firstNeededPageIndex = Math.Max(firstNeededPageIndex, 0);
		    lastNeededPageIndex  = Math.Min(lastNeededPageIndex, this.imageCount - 1);
		    
		    // Recycle no-longer-visible pages 
		    foreach (var page in visiblePages)
			{
		        if (page.index < firstNeededPageIndex || page.index > lastNeededPageIndex) 
				{
					recycledPages.Add(page);
		            page.RemoveFromSuperview();
		        }
		    }
			
			// [visiblePages minusSet:recycledPages];
			foreach (var item in recycledPages)
			{
				if (visiblePages.Contains(item))
				{
					visiblePages.Remove(item);
				}
			}
		  
		    // add missing pages
		    for (int index = firstNeededPageIndex; index <= lastNeededPageIndex; index++) 
			{
		        if (!this.isDisplayingPageForIndex(index))
				{
		            ImageScrollView page = this.dequeueRecycledPage();
		            if (page == null) 
					{
		                page = new ImageScrollView(frameForPageAtIndex(index));	// different from ObjC [CD]
		            }
					this.configurePage (page, index);
					pagingScrollView.AddSubview (page); 
		            visiblePages.Add(page);
		        }
		    }    
		}
		
		ImageScrollView dequeueRecycledPage ()
		{
			ImageScrollView page = null;
			if (recycledPages.Count > 0)
			{
			    page = (from r in recycledPages
					select r).First();
				
				if (page != null)	// HACK: is this correct ??
				{
			        recycledPages.Remove (page);
			    }
			}
			
		    return page;
		}
		bool isDisplayingPageForIndex (int index)
		{
			bool foundPage = false;
		    foreach (ImageScrollView page in visiblePages.ToArray<ImageScrollView>())
			{
		        if (page.index == index)
				{
		            foundPage = true;
		            break;
		        }
		    }
		    return foundPage;
		}
		
		
		void configurePage (ImageScrollView page, int index)
		{
			page.index = index;
		    page.Frame = this.frameForPageAtIndex (index);
		    
			if (useTiledImage)
			{
			    // Use tiled images
				page.displayTiledImageNamed (this.imageNameAtIndex (index), this.imageSizeAtIndex(index));
			} 
			else
			{
			    // To use full images instead of tiled images, replace the "displayTiledImageNamed:" call
			    // above by the following line:
			    page.displayImage (this.imageAtIndex (index));
			}
		}
		#endregion
		
		#region View controller rotation methods
		public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
		{
			return true;
		}
		
		public override void WillRotate (UIInterfaceOrientation toInterfaceOrientation, double duration)
		{
			// here, our pagingScrollView bounds have not yet been updated for the new interface orientation. So this is a good
		    // place to calculate the content offset that we will need in the new orientation
		    var offset = pagingScrollView.ContentOffset.X;
		    var pageWidth = pagingScrollView.Bounds.Size.Width;
		    
		    if (offset >= 0) 
			{
		        firstVisiblePageIndexBeforeRotation = (int)Math.Floor(offset / pageWidth);
		        percentScrolledIntoFirstVisiblePage = (offset - (firstVisiblePageIndexBeforeRotation * pageWidth)) / pageWidth;
		    } 
			else 
			{
		        firstVisiblePageIndexBeforeRotation = 0;
		        percentScrolledIntoFirstVisiblePage = offset / pageWidth;
		    }   
		}
		
		public override void WillAnimateRotation (UIInterfaceOrientation toInterfaceOrientation, double duration)
		{
			// recalculate contentSize based on current orientation
		    pagingScrollView.ContentSize = this.contentSizeForPagingScrollView;
		    
		    // adjust frames and configuration of each visible page
		    foreach (ImageScrollView page in visiblePages.ToArray<ImageScrollView>()) 
			{
		        var restorePoint = page.pointToCenterAfterRotation();
		        var restoreScale = page.scaleToRestoreAfterRotation();
		        page.Frame = this.frameForPageAtIndex (page.index);
		        page.setMaxMinZoomScalesForCurrentBounds();
		        page.restoreCenterPoint(restorePoint, restoreScale);
		        
		    }
		    
		    // adjust contentOffset to preserve page location based on values collected prior to location
		    var pageWidth = pagingScrollView.Bounds.Size.Width;
		    var newOffset = (firstVisiblePageIndexBeforeRotation * pageWidth) + (percentScrolledIntoFirstVisiblePage * pageWidth);
		    pagingScrollView.ContentOffset = new PointF(newOffset, 0);
		}
		#endregion
		
		#region Frame calculations
		//HACK: const int PADDING = 10;
		
		RectangleF frameForPagingScrollView 
		{
			get
			{
				var frame = UIScreen.MainScreen.Bounds;
			    //frame.X -= PADDING; //HACK: remove PADDING
			    //frame.Size = new SizeF(frame.Size.Width + (2 * PADDING), frame.Size.Height); //HACK: remove PADDING
			    return frame;
			}
		}
		
		RectangleF frameForPageAtIndex (int index)
		{
		    // We have to use our paging scroll view's bounds, not frame, to calculate the page placement. When the device is in
		    // landscape orientation, the frame will still be in portrait because the pagingScrollView is the root view controller's
		    // view, so its frame is in window coordinate space, which is never rotated. Its bounds, however, will be in landscape
		    // because it has a rotation transform applied.
		    var bounds = pagingScrollView.Bounds;
		    var pageFrame = bounds;
		    //pageFrame.Size = new SizeF(pageFrame.Size.Width - (2 * PADDING), pageFrame.Size.Height); //HACK: remove PADDING
		    pageFrame.X = (bounds.Size.Width * index);// + PADDING; //HACK: remove PADDING
		    return pageFrame;
		}
		
		SizeF contentSizeForPagingScrollView 
		{
			get
			{
				// We have to use the paging scroll view's bounds to calculate the contentSize, for the same reason outlined above.
				var bounds = pagingScrollView.Bounds;
				return new SizeF(bounds.Size.Width * this.imageCount, bounds.Size.Height);
			}
		}
		
		#endregion
		
		
		#region Image wrangling
		
		static List<ImageInfo> __imageData = null;
		List<ImageInfo> imageData
		{
			get
			{
				
				if (__imageData == null)  // only load the imageData array once
				{
					using (TextReader reader = new StreamReader("ImageData.xml"))
					{
						
					   XmlSerializer serializer = new XmlSerializer(typeof(List<ImageInfo>));
					   __imageData = (List<ImageInfo>)serializer.Deserialize(reader);
					}
					
				}
				if (__imageData == null)
				{
					Console.WriteLine("Failed to read image names. Error...");
				}
				return __imageData;
			}
		}
		
		UIImage imageAtIndex (int index)
		{
			// use "imageWithContentsOfFile:" instead of "imageNamed:" here to avoid caching our images
			var imageName = this.imageNameAtIndex (index);
			//var path = NSBundle.MainBundle.PathForResource(imageName, "jpg");
			var path = "Images/FullImages/" + imageName + ".jpg";	// HACK: resource paths screwed up [CD]
			return UIImage.FromFile(path);
		}
		
		string imageNameAtIndex (int index)
		{
			var name = "";
			if (index < imageData.Count)
			{
				name = imageData[index].Name;
			}
			return name;
		}
		
		SizeF imageSizeAtIndex (int index)
		{
			var size = new SizeF(0,0);
			if (index < imageData.Count)
			{
				size = new SizeF(imageData[index].Width, imageData[index].Height);
			}
			return size;
		}
		
		static int __count = -1;
		int imageCount
		{
			get
			{
				if (__count < 0)
				{
					__count = this.imageData.Count;
				}
				return __count;
			}
		}
		#endregion
	}
	
	/// <summary>
	/// 
	/// </summary>
	public class ImageInfo
	{
		public int Height {get;set;}
		public int Width {get;set;}
		public string Name {get;set;}
	}
}