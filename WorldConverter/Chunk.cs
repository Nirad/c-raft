#region C#raft License
// This file is part of C#raft. Copyright C#raft Team 
// 
// C#raft is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program. If not, see <http://www.gnu.org/licenses/>.
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using Chraft.PluginSystem;
using Chraft.PluginSystem.Entity;
using Chraft.PluginSystem.Net;
using Chraft.PluginSystem.Server;
using Chraft.PluginSystem.World;
using Chraft.PluginSystem.World.Blocks;
using Chraft.Utilities;
using Chraft.Utilities.Blocks;
using Chraft.Utilities.Coords;
using Chraft.Utilities.Misc;
using Chraft.Utilities.Config;
using Ionic.Zlib;
using System.Collections;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace WorldConverter
{
    public class Chunk
    {
        private static object _SavingLock = new object();
        private static volatile bool Saving = false;
        public static readonly int HALFSIZE = 16 * 16 * 128;
        public byte[,] HeightMap { get; private set; }
        internal bool IsRecalculating { get; set; }
        internal volatile bool Deleted;
        private int MaxHeight;
        internal delegate void ForEachBlock(UniversalCoords coords);
        internal NibbleArray Light = new NibbleArray(HALFSIZE);
        internal NibbleArray SkyLight = new NibbleArray(HALFSIZE);
        protected int NumBlocksToUpdate;
        public Section[] _Sections;
        private int _MaxSections;
        public int SectionsToBeUpdated { get; set; }
        private byte[] _BiomesArray = new byte[256];
        public int MaxSections
        {
            get { return _MaxSections; }
        }
        internal Section[] Sections
        {
            get { return _Sections; }
        }

        public void SetLightToRecalculate()
        {

        }

        public void SetType(UniversalCoords coords, BlockData.Blocks value, bool needsUpdate = true)
        {
            int sectionId = coords.BlockY >> 4;
            Section section = _Sections[sectionId];

            if (section == null)
            {
                if (value != BlockData.Blocks.Air)
                    section = AddNewSection(sectionId);
                else
                    return;
            }
            section[coords.SectionPackedCoords] = (byte)value;
            //OnSetType(coords, value);
        }

        public void SetType(int blockX, int blockY, int blockZ, BlockData.Blocks value, bool needsUpdate = true)
        {
            Section section = _Sections[blockY >> 4];

            if (section == null)
            {
                if (value != BlockData.Blocks.Air)
                    section = AddNewSection(blockY >> 4);
                else
                    return;
            }

            section[(blockY & 0xF) << 8 | blockZ << 4 | blockX] = (byte)value;

            //OnSetType(blockX, blockY, blockZ, value);
        }

        public void SetBlockAndData(UniversalCoords coords, byte type, byte data, bool needsUpdate = true)
        {
            Section section = _Sections[coords.BlockY >> 4];

            if (section == null)
            {
                if ((BlockData.Blocks)type != BlockData.Blocks.Air)
                    section = AddNewSection(coords.BlockY >> 4);
                else
                    return;
            }

            section[coords] = type;
            //OnSetType(coords, (BlockData.Blocks)type);

            section.Data.setNibble(coords.SectionPackedCoords, data);
        }

        public void SetBlockAndData(int blockX, int blockY, int blockZ, byte type, byte data, bool needsUpdate = true)
        {
            int blockIndex = (blockY & 0xF) << 8 | blockZ << 4 | blockX;

            Section section = _Sections[blockY >> 4];

            if (section == null)
            {
                if ((BlockData.Blocks)type != BlockData.Blocks.Air)
                    section = AddNewSection(blockY >> 4);
                else
                    return;
            }

            section[blockIndex] = type;

            //OnSetType(blockX, blockY, blockZ, (BlockData.Blocks)type);

            section.Data.setNibble(blockIndex, data);
        }

        public void SetData(UniversalCoords coords, byte value, bool needsUpdate = true)
        {
            Section section = _Sections[coords.BlockY >> 4];

            if (section == null)
            {
                if ((BlockData.Blocks)value != BlockData.Blocks.Air)
                    section = AddNewSection(coords.BlockY >> 4);
                else
                    return;
            }

            section.Data.setNibble(coords.SectionPackedCoords, value);
        }

        public void SetData(int blockX, int blockY, int blockZ, byte value, bool needsUpdate = true)
        {
            Section section = _Sections[blockY >> 4];

            if (section == null)
            {
                if ((BlockData.Blocks)value != BlockData.Blocks.Air)
                    section = AddNewSection(blockY >> 4);
                else
                    return;
            }

            section.Data.setNibble(blockX, blockY & 0xF, blockZ, value);
        }

        public void SetDualLight(UniversalCoords coords, byte value)
        {
            byte low = (byte)(value & 0x0F);
            byte high = (byte)((value & 0x0F) >> 4);

            SkyLight.setNibble(coords.BlockPackedCoords, low);
            Light.setNibble(coords.BlockPackedCoords, high);
        }

        public void SetDualLight(int blockX, int blockY, int blockZ, byte value)
        {
            byte low = (byte)(value & 0x0F);
            byte high = (byte)((value & 0x0F) >> 4);

            SkyLight.setNibble(blockX, blockY, blockZ, low);
            Light.setNibble(blockX, blockY, blockZ, high);
        }

        public void SetBlockLight(UniversalCoords coords, byte value)
        {
            Light.setNibble(coords.BlockPackedCoords, value);
        }

        public void SetBlockLight(int blockX, int blockY, int blockZ, byte value)
        {
            Light.setNibble(blockX, blockY, blockZ, value);
        }

        public void SetSkyLight(UniversalCoords coords, byte value)
        {
            SkyLight.setNibble(coords.BlockPackedCoords, value);
        }

        public void SetSkyLight(int blockX, int blockY, int blockZ, byte value)
        {
            SkyLight.setNibble(blockX, blockY, blockZ, value);
        }

        public void SetBiomeColumn(int x, int z, byte biomeId)
        {
            _BiomesArray[z << 4 | x] = biomeId;
        }

        public byte[] GetBiomesArray()
        {
            return _BiomesArray;
        }

        public Section AddNewSection(int pos)
        {
            Section section = new Section(this, pos);
            _Sections[pos] = section;

            return section;
        }

        public Chunk()
        {
            _Sections = new Section[_MaxSections = 16];
        }

        public int StackSize;

        private byte ChooseHighestNeighbourLight(byte[] lights, out byte vertical)
        {
            vertical = 0;

            // Left
            byte newLight = lights[1];

            // Right
            if (lights[2] > newLight)
                newLight = lights[2];

            // Back
            if (lights[3] > newLight)
                newLight = lights[3];

            // Front
            if (lights[4] > newLight)
                newLight = lights[4];

            // Up
            if ((lights[5] + 1) > newLight)
            {
                newLight = lights[5];
                vertical = 1;
            }

            // Down
            if (lights[6] > newLight)
            {
                newLight = lights[6];
            }

            return newLight;

        }

        private void WriteAllBlocks(Stream strm)
        {
            Queue<Section> toSave = new Queue<Section>();
            int sections = 0;
            for (int i = 0; i < 16; ++i)
            {
                Section section = _Sections[i];

                if (section != null)
                {
                    if (section.NonAirBlocks > 0)
                    {
                        ++sections;
                        toSave.Enqueue(section);
                    }
                    else
                        _Sections[i] = null; // Free some memory 
                }
            }

            //strm.WriteByte((byte)sections);
            Debug.Assert(sections <= 16);
            strm.WriteByte((byte)sections);

            while (toSave.Count > 0)
            {
                Section section = toSave.Dequeue();

                //strm.WriteByte((byte)section.SectionId);
                strm.WriteByte((byte)section.SectionId);
                strm.Write(section.Types, 0, Section.SIZE);
                strm.Write(section.Data.Data, 0, Section.HALFSIZE);
                strm.Write(BitConverter.GetBytes(section.NonAirBlocks), 0, 4);
            }

            strm.Write(Light.Data, 0, HALFSIZE);

            /*World.Logger.Log(LogLevel.Info, "Chunk Write {0} {1}", Coords.ChunkX, Coords.ChunkZ);
            World.Logger.Log(LogLevel.Info, "Skylight: {0}", BitConverter.ToString(SkyLight.Data));
            World.Logger.Log(LogLevel.Info, "----------------------------------------------");*/

            strm.Write(SkyLight.Data, 0, HALFSIZE);
        }
        /*private void Grow(UniversalCoords coords)
        {
            BlockData.Blocks type = GetType(coords);
            byte metaData = GetData(coords);

            if (!(BlockHelper.Instance((byte)type) is IBlockGrowable))
                return;

            UniversalCoords oneUp = UniversalCoords.FromAbsWorld(coords.WorldX, coords.WorldY + 1, coords.WorldZ);
            byte light = GetBlockLight(oneUp);
            byte sky = GetSkyLight(oneUp);

            StructBlock thisBlock = new StructBlock(coords, (byte)type, metaData, this.World);
            IBlockGrowable blockToGrow = (BlockHelper.Instance((byte)type) as IBlockGrowable);
            blockToGrow.Grow(thisBlock);

            switch (type)
            {
                case BlockData.Blocks.Grass:
                    GrowDirt(coords);
                    break;
            }

            if (light < 7 && sky < 7)
            {
                SpawnMob(oneUp);
                return;
            }
            if (type == BlockData.Blocks.Grass)
                SpawnAnimal(coords);
        }*/

        internal void ForAdjacent(UniversalCoords coords, ForEachBlock predicate)
        {
            predicate(UniversalCoords.FromWorld(coords.WorldX - 1, coords.WorldY, coords.WorldZ));
            predicate(UniversalCoords.FromWorld(coords.WorldX + 1, coords.WorldY, coords.WorldZ));
            predicate(UniversalCoords.FromWorld(coords.WorldX, coords.WorldY, coords.WorldZ - 1));
            predicate(UniversalCoords.FromWorld(coords.WorldX, coords.WorldY, coords.WorldZ + 1));
            if (coords.BlockY > 0)
                predicate(UniversalCoords.FromWorld(coords.WorldX, coords.WorldY - 1, coords.WorldZ));
            if (coords.BlockY < 127)
                predicate(UniversalCoords.FromWorld(coords.WorldX, coords.WorldY + 1, coords.WorldZ));
        }

        internal void ForNSEW(UniversalCoords coords, ForEachBlock predicate)
        {
            predicate(UniversalCoords.FromWorld(coords.WorldX - 1, coords.WorldY, coords.WorldZ));
            predicate(UniversalCoords.FromWorld(coords.WorldX + 1, coords.WorldY, coords.WorldZ));
            predicate(UniversalCoords.FromWorld(coords.WorldX, coords.WorldY, coords.WorldZ - 1));
            predicate(UniversalCoords.FromWorld(coords.WorldX, coords.WorldY, coords.WorldZ + 1));
        }
    }

}
