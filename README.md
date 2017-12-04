# KinectShowreel

Kinect Showreel is based on the green screen demo released in the Microsoft Kinect SDK samples. I made some changes (nothing sophisticated) to improve the contours but it's still very flickery on the hair. The reason it works well with me is because I don't have much hair really. :D

For now, the application is able to load images or videos. I have ideas on how to extend the functionalities (e.g. gesture control) but I would like to guage the level of interest. There are some windows functionalities that are buggy. Don't resize the windows and don't move the window while the application is recording. You've been warned... :)

For powerpoint presentations, you'll need to save your powerpoint as a video (wmv) before you can load it within the application. You can use the pause button to pause presentations while you are speaking. You can rewind as needed. Forward button can be buggy in certain situations, which I'm yet to figure out.

It's free for non-commercial use and feel free to send me your updates to the code so I can integrate it. At this point, I'd like to give a shout out to Karl Sanford's project, which I used in my project to try and improve the noisy pixels around the holes and contours.


Requires:
1. Microsoft .NET Framework 4.0 and above
2. Microsoft Expression Encoder 4.0 SP2
3. Kinect for Windows Runtime v1.7 (includes drivers) 
