using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Chraft.World;

namespace Chraft.Net.Packets
{
    public class MapChunkBulkPacket : Packet
    {
        public List<Chunk> ChunksToSend { get; private set; }

        private Queue<MapChunkData> _mapChunksData;

        public MapChunkBulkPacket()
        {
            ChunksToSend = new List<Chunk>();
            _mapChunksData = new Queue<MapChunkData>();
        }

        public override void Read(PacketReader stream)
        {
        }

        public override void Write()
        {
            int totalDataDim = ChunksToSend.Count * 16 * (Section.BYTESIZE + Section.SIZE) + (ChunksToSend.Count * 256);
            byte[] totalData = new byte[totalDataDim];
            int index = 0;

            for (int i = 0; i < ChunksToSend.Count();i++ )
            {
                MapChunkData chunkData = MapChunkPacket.GetMapChunkData(ChunksToSend[i], true);
                _mapChunksData.Enqueue(chunkData);
                Buffer.BlockCopy(chunkData.Data, 0, totalData, index, chunkData.Data.Length);
                index += chunkData.Data.Length;
            }

            int length;
            byte[] compressedData = MapChunkPacket.CompressChunkData(totalData, index, out length);

            SetCapacity(7 + length + (12 * ChunksToSend.Count));

            Writer.Write((short)ChunksToSend.Count);
            Writer.Write(length);
            Writer.Write(compressedData, 0, length);

            for (int i = 0; i < ChunksToSend.Count(); i++)
            {
                MapChunkData chunkData = _mapChunksData.Dequeue();

                Writer.Write(ChunksToSend[i].Coords.ChunkX);
                Writer.Write(ChunksToSend[i].Coords.ChunkZ);
                Writer.Write((short)chunkData.PrimaryBitMask);
                Writer.Write((short)chunkData.AddBitMask);
            }
        }
    }
}