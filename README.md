# Calculate Video DVD Disc Id

Cataloguing software use an algorithm to identify discs that are entered into a PC's drive. These algorithms are different for audio CDs, video DVDs and video Blu-rays. 

For DVDs a programmer can use the IDvdInfo2::GetDiscID interface (https://docs.microsoft.com/en-us/windows/win32/api/strmif/nf-strmif-idvdinfo2-getdiscid)

This interface used to work on Windows XP, Vista, Windows 7, Windows 8.x and Windows 10 until Windows 10 version 1809. 

With Windows 10 version 1809 Microsoft changed the way the disc Id is calculated and effectively broke many of these cataloging softwares. A bug report was filed (https://aka.ms/AA6144x).

The original algorithm is based on US patent 6,871,012 B1 (http://patentimages.storage.googleapis.com/pdfs/US6871012.pdf).

This C# code recreates the original calculation of the Disc Id even on a Windows 10 version 1809 and higher but also shows how the new algorithm works.
