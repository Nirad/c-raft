using Chraft.Utilities.Blocks;
using Ionic.Zlib;
using Substrate;
using Substrate.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            string srcdir = "Source";

            string dstdir = "Destination";

            string WorldName = "test";

            if (!Directory.Exists(srcdir))  // if it doesn't exist, create
            {
                Directory.CreateDirectory(srcdir);
                Console.WriteLine("Source directory created.");
            }

            if (!Directory.Exists(dstdir))  // if it doesn't exist, create
            {
                Directory.CreateDirectory(dstdir);
                Console.WriteLine("Destination directory created.");
            }

            while (!Directory.Exists(srcdir + "/" + WorldName))
            {
                Console.WriteLine("Please put the world in the source directory!");
                Console.Write("Enter the name of your world: ");
                WorldName = Console.ReadLine();
            }

            Directory.CreateDirectory(dstdir + "/" + WorldName);
            Console.WriteLine("Destination World directory created.");
            // Open our world
            NbtWorld world = NbtWorld.Open(srcdir + "/" + WorldName);

            // The chunk manager is more efficient than the block manager for
            // this purpose, since we'll inspect every block
            IChunkManager cm = world.GetChunkManager();
            int block = 0;

            foreach (ChunkRef chunk in cm)
            {
                Console.WriteLine("Processed Chunk {0},{1}", chunk.X, chunk.Z);
                Console.WriteLine("{0:# ### ### ###} block writed", block);
                string DataFile = dstdir+"/"+ WorldName + "/x" + chunk.X + "_z" + chunk.Z + ".gz";
                Chunk c = new Chunk();
                // You could hardcode your dimensions, but maybe some day they
                // won't always be 16.  Also the CLR is a bit stupid and has
                // trouble optimizing repeated calls to Chunk.Blocks.xx, so we
                // cache them in locals
                int xdim = chunk.Blocks.XDim;
                int ydim = chunk.Blocks.YDim;
                int zdim = chunk.Blocks.ZDim;

                // x, z, y is the most efficient order to scan blocks (not that
                // you should care about internal detail)
                for (int x = 0; x < xdim; x++)
                {
                    for (int z = 0; z < zdim; z++)
                    {
                        for (int y = 0; y < ydim; y++)
                        {
                            var type = chunk.Blocks.GetID(x, y, z);
                            var data = chunk.Blocks.GetData(x, y, z);
                            var light = chunk.Blocks.GetBlockLight(x,y,z);
                            var skylight = chunk.Blocks.GetSkyLight(x,y,z);
                            c.SetSkyLight(x, y, z, (byte)skylight);
                            c.SetType(x, y, z, (BlockData.Blocks)type);
                            c.SetData(x, y, z, (byte)data);
                            c.SetBlockLight(x, y, z, (byte)light);
                            block++;
                        }
                    }
                }

                Stream zip = new DeflateStream(File.Create(DataFile + ".tmp"), CompressionMode.Compress);
                try
                {
                    zip.WriteByte(0); // version

                    zip.WriteByte(0);//light
                    for (int x = 0; x < 16; ++x)
                    {
                        for (int z = 0; z < 16; ++z)
                        {
                            zip.WriteByte((byte)chunk.Blocks.GetHeight(x, z));
                        }
                    }

                    Queue<Section> toSave = new Queue<Section>();
                    int sections = 0;
                    for (int i = 0; i < 16; ++i)
                    {
                        Section section = c.Sections[i];

                        if (section != null)
                        {
                            if (section.NonAirBlocks > 0)
                            {
                                ++sections;
                                toSave.Enqueue(section);
                            }
                            else
                                c.Sections[i] = null; // Free some memory 
                        }
                    }

                    zip.WriteByte((byte)sections);

                    while (toSave.Count > 0)
                    {
                        Section section = toSave.Dequeue();

                        //strm.WriteByte((byte)section.SectionId);
                        zip.WriteByte((byte)section.SectionId);
                        zip.Write(section.Types, 0, Section.SIZE);
                        zip.Write(section.Data.Data, 0, Section.HALFSIZE);
                        zip.Write(BitConverter.GetBytes(section.NonAirBlocks), 0, 4);
                    }

                    zip.Write(c.Light.Data, 0, Chunk.HALFSIZE);
                    zip.Write(c.SkyLight.Data, 0, Chunk.HALFSIZE);
                    zip.Flush();
                }
                finally
                {
                    try
                    {
                        zip.Dispose();
                        File.Delete(DataFile);
                        File.Move(DataFile + ".tmp", DataFile);
                    }
                    catch
                    {
                    }
                    finally
                    {
                    }
                }
            }
            Console.WriteLine("An amazing number of {0:# ### ### ###} block writed!", block);
            Console.ReadLine();
        }
    }
}
