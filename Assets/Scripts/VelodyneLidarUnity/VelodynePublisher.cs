﻿using System;

namespace RosSharp.RosBridgeClient
{
    public class VelodynePublisher : UnityPublisher<MessageTypes.Velodyne.VelodyneScan>
    {
        private Lidar lidar;
        public string FrameId = "velodyne";
        private MessageTypes.Velodyne.VelodyneScan message;
        private MessageTypes.Velodyne.VelodynePacket packet;
        private int[] laserIdxs1 = { 0,8 ,1,9, 2,10, 3,11, 4,12, 5,13, 6,14,  7, 15};
        public int numDataBlocks = 12;
        public static DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public virtual MessageTypes.Std.Time Now()
        {
            TimeSpan timeSpan = DateTime.Now.ToUniversalTime() - UNIX_EPOCH;

            double msecs = timeSpan.TotalMilliseconds;
            uint sec = (uint)(msecs / 1000);

            return new MessageTypes.Std.Time
            {
                secs = sec,
                nsecs = (uint)((msecs / 1000 - sec) * 1e+9)
            };
        }

        protected override void Start()
        {
            base.Start();
            lidar = gameObject.GetComponent<Lidar>();
            InitializeMessage();
        }

        private void InitializeMessage()
        {
            message = new MessageTypes.Velodyne.VelodyneScan();
            packet = new MessageTypes.Velodyne.VelodynePacket();
            message.header.frame_id = FrameId;
        }

        public byte[] makeAzimuthBytes(float az)
        {
            ushort azimuth = (ushort)(az * 100.0f);
            //Console.Write("azimuth : {0} -->", azimuth);
            byte[] azimuthArr = System.BitConverter.GetBytes(azimuth);
            //Console.WriteLine("Hex: {0:X}", ByteArrayToString(azimuthArr));
            return azimuthArr;
        }

        public byte[] makeDistanceBytes(float dist)
        {
            ushort distance = (ushort)(dist / 0.002f);
            //Console.Write("distance : {0} ", distance);
            byte[] distArr = System.BitConverter.GetBytes(distance);
            //Console.WriteLine("Hex: {0:X}", ByteArrayToString(distArr));
            return distArr;
        }

        public byte[] Serialize(float[] distanceData, float[] azimuth, int azimutStart, int numLayers, int numIncrements)
        {
            byte[] result = new byte[1206];
            byte[] azimuthArr;
            byte[] distanceArr;

            int dbIdx = 0;
            int azIdx = azimutStart;
            int distIdx;

            for (int db = 0; db < 12; db++)
            {
                if (azIdx >= numIncrements)
                {
                    azIdx = 0;
                }
                //Debug.Log("azIdx " + azIdx + " dbIdx " + db + "\n");

                distIdx = azIdx * numLayers;

                // write a data block
                dbIdx = db * 100;
                result[dbIdx + 0] = 0xff;
                result[dbIdx + 1] = 0xee;
                azimuthArr = makeAzimuthBytes(azimuth[azIdx]);
                Buffer.BlockCopy(azimuthArr, 0, result, dbIdx + 2, 2);
                //Debug.Log("db "+db +" azimut " + azimuth[azIdx]);

                // write channel data, first firing
                for (int c1 = 0; c1 < 16; c1++)
                {
                    distanceArr = makeDistanceBytes(distanceData[distIdx + laserIdxs1[c1]]);
                    //Debug.Log("dist1[ " + (distIdx + c1) + "] " + distanceData[distIdx + c1]+ " for idx "+ laserIdxs1[c1] + " mapped "+ distanceData[distIdx + laserIdxs1[c1]]);

                    Buffer.BlockCopy(distanceArr, 0, result, dbIdx + 4 + c1 * 3, 2);
                    result[dbIdx + 4 + c1 * 3 + 2] = 0xff;
                }
                // write channel data, 2nd firing
                for (int c2 = 16; c2 < 32; c2++)
                {
                    distanceArr = makeDistanceBytes(distanceData[distIdx + laserIdxs1[c2 -16]]);
                    //Debug.Log("dist2[ " + (distIdx + c2)+"] " + distanceData[distIdx + c2]);

                    Buffer.BlockCopy(distanceArr, 0, result, dbIdx + 4 + c2 * 3, 2);
                    result[dbIdx + 4 + c2 * 3 + 2] = 0x12;
                }

                //update idxs
                azIdx = azIdx + 2;
            }
            result[1200] = 0x00;
            result[1201] = 0x00;
            result[1202] = 0x00;
            result[1203] = 0x00;
            result[1204] = 0x37;
            result[1205] = 0x22;
            return result;
        }

        private void Update()
        {
            Boolean cont = true;
            int idx = 0;
            int azIncrPerMsg = 2 * numDataBlocks;
            message.packets.Initialize();
            Array.Resize(ref message.packets, 0);
            while (cont)
            {          
                //Debug.Log("start with IDx "+idx+" at "+Time.time);
                packet.data = Serialize(lidar.distances, lidar.azimuts, idx, lidar.numberOfLayers, lidar.numberOfIncrements);
                Array.Resize(ref message.packets, message.packets.Length + 1);
                message.packets[message.packets.Length - 1] = packet;
                idx = idx + azIncrPerMsg;
                if (idx > (lidar.numberOfIncrements-1))
                {
                    idx = idx - lidar.numberOfIncrements;
                    cont = false;
                }
            }

            message.header.stamp = Now();
            Publish(message);
        }
    }
}
