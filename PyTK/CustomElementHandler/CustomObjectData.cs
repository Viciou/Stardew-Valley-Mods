﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PyTK.Extensions;
using PyTK.Types;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using SObject = StardewValley.Object;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PyTK.CustomElementHandler
{
    public class CustomObjectData
    {
        internal static IModHelper Helper { get; } = PyTKMod._helper;
        internal static IMonitor Monitor { get; } = PyTKMod._monitor;
        internal static int minIndex = 1000;

        public static Dictionary<string,CustomObjectData> collection = new Dictionary<string, CustomObjectData>();
        internal static EventHandler<EventArgsInventoryChanged> inventoryCheck;

        public string id;
        public string data;
        public Texture2D texture;
        public Color color;
        public int tileIndex;

        public Texture2D sdvTexture
        {
            get
            {
                return bigCraftable ? Game1.bigCraftableSpriteSheet : Game1.objectSpriteSheet;
            }
        }

        private Item _obj;
        
        public Rectangle sourceRectangle
        {
            get
            {
                return bigCraftable ? Game1.getSourceRectForStandardTileSheet(texture, tileIndex, 16, 32) : Game1.getSourceRectForStandardTileSheet(texture, tileIndex, 16, 16);
            }
        }
        public Rectangle sdvSourceRectangle
        {
            get
            {
                return bigCraftable ? Game1.getSourceRectForStandardTileSheet(Game1.bigCraftableSpriteSheet, sdvId, 16, 32) : Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, sdvId, 16, 16);
            }
        }
        public bool bigCraftable { get; set; }
        public CraftingData craftingData;
        private int _sdvId = -1;
        public int sdvId
        {
            get
            {
                if (_sdvId == -1 && collection.ContainsKey(id))
                    _sdvId = collection[id]._sdvId;

                if (_sdvId == -1)
                    _sdvId = getNewSDVId();

                if (bigCraftable && _sdvId != -1)
                    if (Game1.bigCraftablesInformation.ContainsKey(_sdvId) && Game1.bigCraftablesInformation[_sdvId] != data)
                        _sdvId = getNewSDVId();

                if (!bigCraftable && _sdvId != -1)
                    if (Game1.objectInformation.ContainsKey(_sdvId) && Game1.objectInformation[_sdvId] != data)
                        _sdvId = getNewSDVId();

                if (craftingData != null)
                    craftingData.index = _sdvId;

                return _sdvId;
            }

            set
            {
                _sdvId = value;
            }
        }

        public string type { get; set; }

        public CustomObjectData(string id, string data, Texture2D texture, Color color, int tileIndex = 0, bool bigCraftable = false, Type type = null, CraftingData craftingData = null)
        {
            this.id = id;
            this.data = data;
            this.texture = texture;
            this.tileIndex = tileIndex;
            this.bigCraftable = bigCraftable;
            this.color = color;
            type = type != null ? type : typeof(PySObject);
            string[] typeData = type.AssemblyQualifiedName.Split(',');
            this.type = typeData[0] + ", " + typeData[1];
            this.craftingData = craftingData;

            if(craftingData != null)
                craftingData.bigCraftable = bigCraftable;

            sdvId = getNewSDVId();

            collection.AddOrReplace(id, this);

            if(inventoryCheck == null)
                inventoryCheck = new ItemSelector<Item>(i => collection.Exists(c => c.Value.sdvId == i.parentSheetIndex && (!(i is SObject sobj) || sobj.bigCraftable == c.Value.bigCraftable))).whenAddedToInventory(l => l.useAll(x => Game1.player.items[Game1.player.items.FindIndex(o => o == x)] = collection.Find(c => c.Value.sdvId == x.parentSheetIndex && (!(x is SObject sobj) || sobj.bigCraftable == c.Value.bigCraftable)).Value.getObject(x)));
        }

        public int getIndexForId(string id)
        {
            return collection.Find(c => c.Value.id == id).Value.sdvId;
        }

        public Item replaceItem(Item item)
        {
            if (!collection.Exists(c => c.Value.sdvId == item.parentSheetIndex))
                return item;

            return collection.Find(c => c.Value.sdvId == item.parentSheetIndex).Value.getObject();
        }

        public int getNewSDVId()
        {
            int newIndex = -1;

            if (bigCraftable)
            {
                newIndex = (Game1.bigCraftablesInformation.ContainsKey(_sdvId) && Game1.bigCraftablesInformation[_sdvId] == data) ? _sdvId : Math.Max(Math.Max(Game1.bigCraftablesInformation.Keys.Max() + 1, Game1.objectInformation.Keys.Max() + 1), minIndex);
                Game1.bigCraftablesInformation.AddOrReplace(newIndex, data);
            }
            else
            {
                newIndex = (Game1.objectInformation.ContainsKey(_sdvId) && Game1.objectInformation[_sdvId] == data) ? _sdvId : Math.Max(Math.Max(Game1.bigCraftablesInformation.Keys.Max() + 1, Game1.objectInformation.Keys.Max() + 1), minIndex);
                Game1.objectInformation.AddOrReplace(newIndex, data);
            }

            return newIndex;
        }

        public Item getObject(Item item = null)
        {
            if (item is ICustomObject)
                return item;

            if (_obj == null)
            {
                try
                {
                    Type T = Type.GetType(type);

                    if (T == null)
                        return null;

                    _obj = (Item)Activator.CreateInstance(T, new object[] { this });
                }
                catch (Exception e)
                {
                    Monitor.Log("Exception while building Custom Object: " + e.Message, LogLevel.Error);
                    Monitor.Log("CustomObjectData:getObject:" + id + ":" + e.StackTrace, LogLevel.Error);
                }
            }

            if (_obj == null)
                return null;

            Item result = _obj.getOne();

            if (item != null)
            {
                result.Stack = item.Stack;

                if (item is SObject sobj && result is SObject)
                {
                    (result as SObject).quality = sobj.quality;
                    (result as SObject).price = sobj.price;
                    (result as SObject).name = sobj.name;
                }
            }

            return result;
        }

        public static CustomObjectData newObject(string uniqueId, Texture2D texture, Color color, string name, string description, int tileIndex = 0, string displayName = "", string type = "Basic", int price = 100, int edibility = -300, string typeInfo = "", CraftingData craftingData = null, Type customType = null)
        {
            if (displayName == "")
                displayName = name;

            List<string> data = new List<string> { name, price.ToString(), edibility.ToString(), type, displayName, description };
            if (typeInfo != null && typeInfo != "")
                data.Add(typeInfo);

            return new CustomObjectData(uniqueId, String.Join("/", data), texture, color, tileIndex, false, customType, craftingData);
        }

        public static CustomObjectData newBigObject(string uniqueId, Texture2D texture, Color color, string name, string description, int tileIndex = 0, string displayName = "", bool lamp = false, int fragility = 0, bool indoors = true, bool outdoors = true, string type = "Crafting -9", int price = 100, int edibility = -300, CraftingData craftingData = null, Type customType = null)
        {
            if (displayName == "")
                displayName = name;

            List<string> data = new List<string> { name, price.ToString(), edibility.ToString(), type, description, indoors.ToString().ToLower(), outdoors.ToString().ToLower(), fragility.ToString() };
            if (lamp)
                data.Add("true");

            data.Add(displayName);

            return new CustomObjectData(uniqueId, String.Join("/", data), texture, color, tileIndex, true, customType, craftingData);
        }
        
    }
}
