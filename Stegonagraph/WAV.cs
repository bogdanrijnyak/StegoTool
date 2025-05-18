using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stegonagraph
{
    class WAV
    {
        public int NumberOfChannels { set; get; }
        public int BlockAlignBytes { set; get; }
        public int BitsPerSample { set; get; }
        public UInt64 StartPos { set; get; }
        public UInt64 AudioInfoCount { set; get; }
        private byte[] wavFile;

        public WAV(String filePath)
        {
            this.wavFile = File.ReadAllBytes(filePath);

            String binaryStr = "";
            UInt64 cursor = 0;

            cursor += 22;

            for (var index = cursor; index < cursor + 2; index++)
            {
                String byteStr = HelpTools.AutoAddByte(Convert.ToString(wavFile[index], 2), 8);
                binaryStr = byteStr + binaryStr;
            }

            this.NumberOfChannels = Convert.ToInt32(binaryStr, 2);
            binaryStr = "";
            cursor += 10;

            for (var index = cursor; index < cursor + 2; index++)
            {
                String byteStr = HelpTools.AutoAddByte(Convert.ToString(wavFile[index], 2), 8);
                binaryStr = byteStr + binaryStr;
            }

            this.BlockAlignBytes = Convert.ToInt32(binaryStr, 2);
            binaryStr = "";
            cursor += 2;

            for (var index = cursor; index < cursor + 2; index++)
            {
                String byteStr = HelpTools.AutoAddByte(Convert.ToString(wavFile[index], 2), 8);
                binaryStr = byteStr + binaryStr;
            }

            this.BitsPerSample = Convert.ToInt32(binaryStr, 2);

            while (!(binaryStr == "data" || cursor == (UInt64)wavFile.Length))
            {
                binaryStr = ((char)wavFile[cursor]).ToString() + ((char)wavFile[cursor + 1]).ToString() + ((char)wavFile[cursor + 2]).ToString() + ((char)wavFile[cursor + 3]).ToString();
                cursor++;
            }

            binaryStr = "";
            cursor += 3;

            for (var index = cursor; index < cursor + 4; index++)
            {
                String byteStr = HelpTools.AutoAddByte(Convert.ToString(wavFile[index], 2), 8);
                binaryStr = byteStr + binaryStr;
            }

            cursor += 4;
            this.AudioInfoCount = Convert.ToUInt64(binaryStr, 2);
            this.StartPos = cursor;
        }

        public void WavEncode(List<Byte> hiddenData, String outputPath, byte[] keyArray)
        {
            UInt64 position = StartPos;
            UInt32 stepIndex = 0;
            String dataBits = "";
            int channelCount = NumberOfChannels == 1 ? 1 : 2;

            for (int dataIndex = 0; dataIndex < hiddenData.Count; dataIndex++)
            {
                dataBits += HelpTools.AutoAddByte(Convert.ToString(hiddenData[dataIndex], 2), 8);

                while (dataBits.Length > (channelCount * keyArray[stepIndex % keyArray.Length]))
                {
                    UInt64 bufferPosition = position;
                    String sampleBits = "";

                    for (int byteIndex = 0; byteIndex < BlockAlignBytes; byteIndex++)
                    {
                        String byteStr = HelpTools.AutoAddByte(Convert.ToString(wavFile[position++], 2), 8);
                        sampleBits = byteStr + sampleBits;
                    }

                    position = bufferPosition;

                    for (int rotate = 0; rotate < BlockAlignBytes * 8 / BitsPerSample - 1; rotate++)
                    {
                        sampleBits = sampleBits.Substring(BitsPerSample) + sampleBits.Substring(0, BitsPerSample);
                    }

                    for (int ch = 0; ch < channelCount; ch++)
                    {
                        String bitField = sampleBits.Substring(0, BitsPerSample);
                        bitField = bitField.Substring(0, bitField.Length - keyArray[stepIndex % keyArray.Length]);
                        bitField += dataBits.Substring(0, keyArray[stepIndex % keyArray.Length]);
                        dataBits = dataBits.Substring(keyArray[stepIndex % keyArray.Length]);

                        for (int bit = bitField.Length; bit > 0; bit -= 8)
                        {
                            wavFile[position++] = Convert.ToByte(bitField.Substring(bit - 8, 8), 2);
                        }

                        sampleBits = sampleBits.Substring(BitsPerSample);
                    }

                    position = bufferPosition + (UInt64)BlockAlignBytes;
                    stepIndex++;
                }
            }

            while (dataBits.Length % (channelCount * keyArray[stepIndex % keyArray.Length]) != 0)
                dataBits += "0";

            while (dataBits.Length != 0)
            {
                UInt64 bufferPosition = position;
                String sampleBits = "";

                for (int byteIndex = 0; byteIndex < BlockAlignBytes; byteIndex++)
                {
                    String byteStr = HelpTools.AutoAddByte(Convert.ToString(wavFile[position++], 2), 8);
                    sampleBits = byteStr + sampleBits;
                }

                position = bufferPosition;

                for (int rotate = 0; rotate < BlockAlignBytes * 8 / BitsPerSample - 1; rotate++)
                {
                    sampleBits = sampleBits.Substring(BitsPerSample) + sampleBits.Substring(0, BitsPerSample);
                }

                for (int ch = 0; ch < channelCount; ch++)
                {
                    String bitField = sampleBits.Substring(0, BitsPerSample);
                    bitField = bitField.Substring(0, bitField.Length - keyArray[stepIndex % keyArray.Length]);
                    bitField += dataBits.Substring(0, keyArray[stepIndex % keyArray.Length]);
                    dataBits = dataBits.Substring(keyArray[stepIndex % keyArray.Length]);

                    for (int bit = bitField.Length; bit > 0; bit -= 8)
                    {
                        wavFile[position++] = Convert.ToByte(bitField.Substring(bit - 8, 8), 2);
                    }

                    sampleBits = sampleBits.Substring(BitsPerSample);
                }

                position = bufferPosition + (UInt64)BlockAlignBytes;
                stepIndex++;
            }

            File.WriteAllBytes(outputPath, this.wavFile);
        }

        public List<byte> WavDecode(int expectedLength, byte[] keyArray)
        {
            UInt64 position = StartPos;
            UInt32 stepIndex = 0;
            List<byte> extractedData = new List<byte>();
            String bitStream = "";
            int channelCount = NumberOfChannels == 1 ? 1 : 2;

            while (extractedData.Count != expectedLength)
            {
                UInt64 bufferPosition = position;
                String sampleBits = "";

                for (int byteIndex = 0; byteIndex < BlockAlignBytes; byteIndex++)
                {
                    String byteStr = HelpTools.AutoAddByte(Convert.ToString(wavFile[position++], 2), 8);
                    sampleBits = byteStr + sampleBits;
                }

                position = bufferPosition;

                for (int rotate = 0; rotate < BlockAlignBytes * 8 / BitsPerSample - 1; rotate++)
                    sampleBits = sampleBits.Substring(BitsPerSample) + sampleBits.Substring(0, BitsPerSample);

                for (int ch = 0; ch < channelCount; ch++)
                {
                    String bitField = sampleBits.Substring(0, BitsPerSample);
                    bitStream += bitField.Substring(bitField.Length - keyArray[stepIndex % keyArray.Length], keyArray[stepIndex % keyArray.Length]);
                    sampleBits = sampleBits.Substring(BitsPerSample);
                }

                position = bufferPosition + (UInt64)BlockAlignBytes;
                stepIndex++;

                while (bitStream.Length >= 8)
                {
                    extractedData.Add(Convert.ToByte(bitStream.Substring(0, 8), 2));
                    bitStream = bitStream.Substring(8);
                }
            }

            return extractedData;
        }
    }
}
