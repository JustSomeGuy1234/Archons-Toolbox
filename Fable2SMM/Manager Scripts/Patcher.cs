using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;

namespace Fable2SMM.Patching
{
    class Patcher
    {

        // By https://steamcommunity.com/id/NotJustSomeGuy/
        // https://github.com/JustSomeGuy1234/
        // JustSomeGuy (Discord)

        // A Patcher I made a few years ago for fun. It's inefficient, badly written, doesn't support 4GiB+ files, and there's no compression, but I use it anyway.
        // Not very useful for actually updating applications, but instead to solve potential legal issues with redistributing entire game files.




        // This bool controls whether the build is for release or for creating a patch.
        private static bool GameSpecific = false;


        static public void GeneratePatch(string pathOrig, string pathModded, string pathOutput)
        {
            if (pathOrig == "" || pathModded == "")
                throw new Exception("ERROR: One of the paths to the files is empty.");
            
            Trace.WriteLine("Started generating patch at " + DateTime.Now);
            pathOrig = pathOrig.Replace("\"", "");
            pathModded = pathModded.Replace("\"", "");

            FileStream origFileStream = File.OpenRead(pathOrig);
            FileStream moddedFileStream = File.OpenRead(pathModded);
            // Read all of the original and modded file's bytes into arrays
            byte[] origFileBytes = new byte[(int)origFileStream.Length];    
            origFileStream.Read(origFileBytes, 0, (int)origFileStream.Length);
            byte[] moddedFileBytes = new byte[(int)moddedFileStream.Length];    
            moddedFileStream.Read(moddedFileBytes, 0, (int)moddedFileStream.Length);

            // Use this to store the offset of the current bytes, but we need to optimize its size first. I declare its size here to keep the compiler happy.
            byte[] offset = new byte[1];
            // this is used during the optimization of the offset array size
            byte[] temp; 
            bool reachedEndOfOrig = false;
            List<byte> consecutive = new List<byte>(2048);
            // Create the empty patch file
            File.Delete(pathOrig + ".patch");
            FileStream patchFileStream;
            try
                { patchFileStream = File.OpenWrite(pathOutput); }
            catch(Exception ex)
                { System.Windows.MessageBox.Show("Failed to write to output path:\n" + ex.Message); return; }

            bool wasLastByteOfOrigFileSame = false; 
            // Loop gets every byte in the modded file, and compares them with the bytes in the unmodified "original" file, then writes them to a file alongside their offsets.
            // If one byte after another is different then they get added to the "consecutive" list so we may store only one offset for a long number of bytes.
            // This is certainly inefficient because for every non-consecutive but different byte, we end up writing extra data about the length of the consecutive "streak" anyway.
            for (int i = 0; i < moddedFileBytes.Length; i++)
            {
                if (!reachedEndOfOrig && i >= origFileBytes.Length)
                {

                    if (consecutive.Count == 0)
                        wasLastByteOfOrigFileSame = true;

                    reachedEndOfOrig = true;
                }
                if (!reachedEndOfOrig && origFileBytes[i] != moddedFileBytes[i])
                {
                    if (consecutive.Count < 1) // If this is the beginning of a new streak then we store the offset. If it isn't the first byte of a streak then we already have the offset set from processing the first byte of this streak.
                        offset = TrimIntToBytes(i);

                    consecutive.Add(moddedFileBytes[i]); // Since the byte is different, we add it to the consecutive list. The offset is now stored in the offset variable whether it was set just then or in a previous loop.
                    
                }
                else if(!reachedEndOfOrig && origFileBytes[i] == moddedFileBytes[i] && consecutive.Count != 0) // Upon ending the streak; write the num of bytes, the bytes, num of offset bytes and offset.
                {
                    temp = TrimIntToBytes(consecutive.Count); // Put the LENGTH of consecutive list into a byte array.
                    patchFileStream.WriteByte((byte)temp.Length); // Then write the LENGTH OF THAT.
                    patchFileStream.Write(temp, 0, temp.Length); // Then write the length of the consecutive bytes
                    patchFileStream.Write(consecutive.ToArray(), 0, consecutive.Count); // Then write the bytes
                    patchFileStream.WriteByte((byte)offset.Length); // Write the offset length which we trimmed earlier. We DON'T write the length of the length though because it can read a max of four bytes anyway, which is fine unless we're reading a 4GiB file.
                    patchFileStream.Write(offset, 0, offset.Length); // offset was reversed earlier at the start of the streak so no need to reverse it again
                    consecutive.Clear();
                }
                if (reachedEndOfOrig)
                {
                    consecutive.Add(moddedFileBytes[i]); // If we've passed the end of the origfile then every byte from here forwards IS different and therefor added to the list. This will get written when we've hit the end of the moddedFileBytes array.
                    
                }
            }

            // This was the solution to a devious little issue that took me a few hours to find where the patcher worked only half the time. The problem was that the consecutive streak never ended if the last byte was the same.
            if (consecutive.Count > 0) // This is reached once the loop is ended. I think this is for when the end of the modded file is reached, as the only other time bytes are written is when a streak comes to an end. Obviously a streak won't end if there's nothing to end it.
            {
                // We must set offset to the end of the file if the last byte of both files is the same, otherwise offset does not get updated if the last byte in both files is the same. (Otherwise, offset will be left as the last offset of the last differing byte/s)
                // If the last byte is not the same, we are still part of a previous streak and as such will not change the offset.
                if (!GameSpecific)
                {
                    Trace.WriteLine("Going to write last " + consecutive.Count + " bytes at " + origFileBytes.Length);
                }
                if (wasLastByteOfOrigFileSame)
                    offset = TrimIntToBytes(origFileBytes.Length); // Issue: Maybe?

                // Read above comments for process of writing. It's exactly the same.
                temp = TrimIntToBytes(consecutive.Count); 
                patchFileStream.WriteByte((byte)temp.Length);
                patchFileStream.Write(temp, 0, temp.Length);
                patchFileStream.Write(consecutive.ToArray(), 0, consecutive.Count);

                // Offset max size is 4 bytes. If it's more then we will get an error but 4 bytes suffices for files smaller than 4GiB
                patchFileStream.WriteByte((byte)offset.Length); 
                patchFileStream.Write(offset, 0, offset.Length); // offset was reversed earlier at the start of the streak
                consecutive.Clear();
            }
            // The patch has been generated. All there is to do now is write the filesize.
            // I reuse the offset variable for this purpose. This writes the end size that any patched files will be resized to. (see the first few lines of Patch())
            offset = TrimIntToBytes(moddedFileBytes.Length); 
            patchFileStream.Write(offset, 0, offset.Length);
            patchFileStream.WriteByte((byte)offset.Length);

            Trace.WriteLine("Finished generating patch at " + DateTime.Now);
        }
        
        public static void Patch(string origFilePath, string patchFilePath)
        {
            // Time to do file handling stuff. Good lord these comments are a mess.
            FileStream patchFileStream = new FileStream(patchFilePath, FileMode.Open);
            FileStream origFileStream = File.OpenWrite(origFilePath);

            int numOfBytes;
            int numOfNumOfBytes;
            int numOfOffsetBytes;
            byte[] toWrite;
            byte[] temp = new byte[4]; // This will contain the filesize offset temporarily while we turn it into an int
            byte[] offsetTemp;
            int filesize; // Use this to resize the file after we've written everything
            int filesizeSize;
            int patchFileFilesizeOffset; // Use this to identify four bytes before end of the patch file
            int numOfLoops = 0;

            patchFileStream.Position = patchFileStream.Length - 1; // The filesize-size offset is always 1 byte from the end of the file
            // Find the offset for the filesize var. This will be used to figure out when to stop the loop of reading bytes and offsets, as we don't want it reading this
            filesizeSize = patchFileStream.ReadByte(); // This causes us to go forward once again so we need to go back an extra one for the next pos write
            patchFileStream.Position -= filesizeSize + 1; // we're trying to go back the length of the filesizesize and then go back another one to counter the byte we just read.
            patchFileFilesizeOffset = (int)patchFileStream.Position;
            patchFileStream.Read(temp, 0, filesizeSize);
            filesize = BitConverter.ToInt32(temp, 0); // No need to reverse this because we reversed it when writing it to the file. No need to trim this either because ToInt32 requires four bytes.
            temp = new byte[4]; // reset temp because we just used it to get filesize
            // reset pos after finding it
            patchFileStream.Position = 0;

            while (patchFileStream.Position != (long)patchFileFilesizeOffset)
            {
                // Sequence is: read number of number of bytes - read number of bytes - read those bytes and store them in toWrite - read number of offset bytes - read offset bytes and convert em- set origfilestream pos to offsetToWriteAt - write at that offset using toWrite.
                // So, data info >> data >> offset info

                numOfLoops++; // for debugging
                temp = new byte[4]; // reset this. it can contain bytes in indices that don't get overwritten by Read();
                // Read how many bytes we need to read to figure out how many bytes we're going to read, read them and store it them in toWrite
                numOfNumOfBytes = patchFileStream.ReadByte();
                patchFileStream.Read(temp, 0, numOfNumOfBytes);
                numOfBytes = BitConverter.ToInt32(temp, 0);
                toWrite = new byte[numOfBytes];
                patchFileStream.Read(toWrite, 0, numOfBytes);

                // Do above but for offset this time. We are now PAST the data and it is in toWrite.
                numOfOffsetBytes = patchFileStream.ReadByte(); // IF THIS REACHES EOF THEN IT RETURNS -1
                if (numOfOffsetBytes > 4)
                {
                    throw new ArgumentException("Offset is more than four byte in length. 4GB+ files are not supported.", "OffsetOverflow");
                }
                // Get offset info
                offsetTemp = new byte[4]; // reset this because things can change
                patchFileStream.Read(offsetTemp, 0, numOfOffsetBytes);
                int offsetToWriteAt = BitConverter.ToInt32(offsetTemp, 0);
                origFileStream.Position = (long)offsetToWriteAt;


                // We now have offset and data. Write it.
#if DEBUG
                if (!GameSpecific)
                    Trace.WriteLine("Writing " + toWrite.Length + " bytes at " + origFileStream.Position);
#endif
                origFileStream.Write(toWrite, 0, toWrite.Length); // 4

            }
            // In case of the modded file being smaller than the original, trim the old redundant data from the end of the file.
            origFileStream.SetLength(filesize);


            origFileStream.Close();
            patchFileStream.Close();

            Trace.WriteLine("Finished at " + DateTime.Now);
        }

        // This method turns a four-byte (32 bit) int into the smallest amount of bytes it can fit into. 0-255 will be trimmed to 1 byte, 256-65535 will be 2 bytes etc.
        public static byte[] TrimIntToBytes(int TrimMe) 
        {
            byte[] temp;
            byte[] trimmedIntNowByte = BitConverter.GetBytes(TrimMe);
            for (int trimmerIndex = 0; trimmerIndex < 4; trimmerIndex++) // Turn the offset into an array. Then we will trim the array of any index's that are unused (contain 0x00, except the first one because if that's 0x00 we know that it means index 0)
            {
                if (trimmedIntNowByte[trimmedIntNowByte.Length - 1 - trimmerIndex] == (byte)0)
                {
                    if (trimmerIndex == 3)
                    {
                        temp = new byte[trimmedIntNowByte.Length - trimmerIndex];
                        Array.Copy(trimmedIntNowByte, temp, trimmedIntNowByte.Length - trimmerIndex); // Debugging: Check around this code in case of null bytes once generated
                        trimmedIntNowByte = temp;
                        return trimmedIntNowByte; // We break if we hit an array entry that contains a non-null value as we know that will be part of the offset
                    }
                }
                else
                {
                    temp = new byte[trimmedIntNowByte.Length - trimmerIndex];
                    Array.Copy(trimmedIntNowByte, temp, trimmedIntNowByte.Length - trimmerIndex); // Debugging: Check around this code in case of null bytes once generated
                    trimmedIntNowByte = temp;
                    return trimmedIntNowByte; // We break if we hit an array entry that contains a non-null value as we know that will be part of the offset
                }
            }
            return trimmedIntNowByte;
        }
    }
}
