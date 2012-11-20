using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Chraft.Interfaces;
using Chraft.Net;
using Chraft.Persistence;
using Chraft.Utilities.NBT;
using Chraft.Utilities.Config;
using System.Xml;
using Chraft.Entity.Items.Base;
using Chraft.Entity.Items;

namespace Chraft.Utils
{
    public class PlayerNBTConverter
    {
        /// <summary>
        /// Converts a Minecraft NBT format player file to c#raft xml
        /// </summary>
        /// <param name="fileName">Filepath of nbt</param>
        internal void ConvertPlayerNBT(string fileName)
        {
            FileStream s = null;
            NBTFile nbt = null;
            try
            {
                Player p = new Player();
                s = new FileStream(fileName, FileMode.Open);
                nbt = NBTFile.OpenFile(s, 1);
                p.DisplayName = Path.GetFileNameWithoutExtension(fileName);
                foreach (KeyValuePair<string, NBTTag> sa in nbt.Contents)
                {
                    switch (sa.Key)
                    {
                        case "Health":
                            p.Health = sa.Value.Payload;
                            break;
                        case "Pos":
                            p.X = sa.Value.Payload[2].Payload;
                            p.Y = sa.Value.Payload[1].Payload;
                            p.Z = sa.Value.Payload[0].Payload;
                            break;
                        case "Rotation":
                            p.Pitch = sa.Value.Payload[1].Payload;
                            p.Yaw = sa.Value.Payload[0].Payload;
                            break;
                        case "playerGameType":
                            p.GameMode = (byte)sa.Value.Payload;
                            break;
                        case "foodLevel":
                            p.Food = (short)sa.Value.Payload;
                            break;
                        case "foodSaturationLevel":
                            p.FoodSaturation = sa.Value.Payload;
                            break;
                        case "XpTotal":
                            p.Experience = sa.Value.Payload;
                            break;
                        case "Inventory":
                            Inventory inv = new Inventory();
                            foreach (NBTTag tag in sa.Value.Payload)
                            {
                                inv.AddItem((short)tag.Payload["id"].Payload, (sbyte)tag.Payload["Count"].Payload,
                                            (short)tag.Payload["Damage"].Payload, false);
                            }
                            p.Inventory = inv;
                            break;
                    }
                }
                SavePlayerXml(p, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error converting file" + fileName + " to C#raft format");
                Console.WriteLine(ex);
            }
            finally
            {
                if (s != null)
                    s.Dispose();
                if (nbt != null)
                    nbt.Dispose();
            }
        }

        private void SavePlayerXml(Player p, string fileName)
        {
            var doc = new XmlDocument();

            var dec = doc.CreateXmlDeclaration("1.0", "utf-8", null);
            doc.AppendChild(dec);
            var root = doc.CreateElement("Player");

            var arg = doc.CreateElement("X");
            arg.InnerText = p.X.ToString();
            root.AppendChild(arg);
            arg = doc.CreateElement("Y");
            arg.InnerText = p.Y.ToString();
            root.AppendChild(arg);
            arg = doc.CreateElement("Z");
            arg.InnerText = p.Z.ToString();
            root.AppendChild(arg);
            arg = doc.CreateElement("Yaw");
            arg.InnerText = p.Yaw.ToString();
            root.AppendChild(arg);
            arg = doc.CreateElement("Pitch");
            arg.InnerText = p.Pitch.ToString();
            root.AppendChild(arg);
            arg = doc.CreateElement("Health");
            arg.InnerText = p.Health.ToString();
            root.AppendChild(arg);
            arg = doc.CreateElement("Food");
            arg.InnerText = p.Food.ToString();
            root.AppendChild(arg);
            arg = doc.CreateElement("FoodSaturation");
            arg.InnerText = p.FoodSaturation.ToString();
            root.AppendChild(arg);
            arg = doc.CreateElement("GameMode");
            arg.InnerText = ((byte)p.GameMode).ToString();
            root.AppendChild(arg);
            arg = doc.CreateElement("DisplayName");
            arg.InnerText = p.DisplayName;
            root.AppendChild(arg);
            arg = doc.CreateElement("SightRadius");
            arg.InnerText = p.SightRadius == 0 ? ChraftConfig.MaxSightRadius.ToString() : p.SightRadius.ToString();//TODO use the max of the server or?
            root.AppendChild(arg);
            arg = doc.CreateElement("Experience");
            arg.InnerText = p.Experience.ToString();
            root.AppendChild(arg);

            XmlElement inventoryNode = doc.CreateElement("Inventory");
            ItemInventory item;
            XmlElement itemDoc;

            for (short i = 5; i <= 44; i++)
            {
                if (p.Inventory[i] == null || ItemHelper.IsVoid(p.Inventory[i]))
                    continue;
                item = p.Inventory[i];
                itemDoc = doc.CreateElement("Item");
                itemDoc.SetAttribute("Slot", i.ToString());
                itemDoc.SetAttribute("Type", item.Type.ToString());
                itemDoc.SetAttribute("Count", item.Count.ToString());
                itemDoc.SetAttribute("Durability", item.Durability.ToString());
                inventoryNode.AppendChild(itemDoc);
            }

            root.AppendChild(inventoryNode);
            doc.AppendChild(root);

            string folder = ChraftConfig.PlayersFolder;
            string dataFile = folder + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(fileName) + ".xml";

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            string file = dataFile + ".tmp";
            try
            {
                doc.Save(file);
            }
            catch (IOException)
            {
                return;
            }
            if (File.Exists(dataFile))
                File.Delete(dataFile);
            File.Move(file, dataFile);
            File.Move(fileName, Path.ChangeExtension(fileName, ".conv"));
        }

    }
}

