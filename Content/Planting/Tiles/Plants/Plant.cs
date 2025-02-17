using SAA.Content.Foods;
using SAA.Content.Items;
using SAA.Content.Planting.Seeds;
using SAA.Content.Planting.System;
using SAA.Content.Sys;
using Terraria.Enums;
using Terraria.GameContent.Metadata;

namespace SAA.Content.Planting.Tiles.Plants
{
    public enum PlantStage : byte
    {
        Planted,
        Growing,
        Grown
    }
    public abstract class Plant : ModTile
    {
        /// <summary>
        /// 可以被镰刀收割
        /// </summary>
        public virtual bool CanBeReapedBySickle => true;
        /// <summary>
        /// 生长阶段宽度
        /// </summary>
        protected virtual short FrameWidth => 18;
        /// <summary>
        /// 物块高度, 1/2/3
        /// </summary>
        protected virtual int Height => 2;
        /// <summary>
        /// 生长速度1到100
        /// </summary>
        protected virtual int GrowthRate => 1;
        /// <summary>
        /// 采摘
        /// </summary>
        public virtual bool CanPick => false;
        /// <summary>
        /// 只能采摘一次
        /// </summary>
        public virtual bool PickJustOneTime => false;
        /// <summary>
        /// 不受收成影响
        /// </summary>
        protected virtual bool CropHarvestCantAffect => false;
        /// <summary>
        /// 风中摇摆（每格物块都会摆）
        /// </summary>
        protected virtual bool CanSwayInWind => true;
        /// <summary>
        /// 绘制反向
        /// </summary>
        protected virtual bool FlipHorizontally => true;
        protected virtual int HerbItemType => ModContent.ItemType<海麦>();
        protected virtual int SeedItemType => ModContent.ItemType<海燕麦种子>();
        protected virtual void ModifyTileObjectData() { }
        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileObsidianKill[Type] = true;
            //Main.tileCut[Type] = true;
            Main.tileNoFail[Type] = true;
            //TileID.Sets.ReplaceTileBreakUp[Type] = true;
            TileID.Sets.IgnoredInHouseScore[Type] = true;
            TileID.Sets.IgnoredByGrowingSaplings[Type] = true;
            TileMaterials.SetForTileId(Type, TileMaterials._materialsByName["Plant"]);
            // 让这种瓷砖与高尔夫球互动，就像其他植物一样

            // 我们不使用这个，因为我们的瓷砖应该只有在它完全长大的时候才能被发现。这就是我们使用IsTileSpelunkable钩子的原因
            //Main.tileSpelunker[Type] = true;

            // 不要使用这个，它会引起许多意想不到的副作用
            //Main.tileAlch[Type] = true;

            this.RegisterTile(Color.Green);
            switch (Height)
            {
                case 3:
                    TileObjectData.newTile.CopyFrom(TileObjectData.Style1xX);
                    TileObjectData.newTile.Height = 3;
                    break;
                case 2:
                    TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2);
                    break;
                default:
                    TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
                    break;
            }
            TileObjectData.newTile.AnchorValidTiles = new int[] {
                ModContent.TileType<Arable>(),
            };
            TileObjectData.newTile.WaterPlacement = LiquidPlacement.NotAllowed;
            TileObjectData.newTile.LavaPlacement = LiquidPlacement.NotAllowed;
            ModifyTileObjectData();
            TileObjectData.addTile(Type);
            if (CanSwayInWind) TileID.Sets.SwaysInWindBasic[Type] = true;//随风摇摆
            HitSound = SoundID.Grass;
            DustType = DustID.Grass;
        }
        public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
        {
            if (FlipHorizontally && i % 2 == 0)
            {
                spriteEffects = SpriteEffects.FlipHorizontally;
            }
        }
        public override bool IsTileSpelunkable(int i, int j)
        {
            PlantStage stage = GetStage(i, j);
            return stage == PlantStage.Grown;
        }
        /// <summary>
        /// 尝试让作物生长
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="growMagnification">生长倍率，与生长速度相乘提高生长概率</param>
        /// <param name="needDayTime">需要白天</param>
        /// <param name="needWet">需要耕地潮湿</param>
        public virtual void TryGrow(int i, int j, int growMagnification = 1, bool needDayTime = true, bool needWet = true)
        {
            Tile land = Framing.GetTileSafely(i, j + 1);
            bool flag = land.TileType == ModContent.TileType<Arable>();
            if (!flag) return;//底端物块有效
            PlantStage stage = GetStage(i, j);
            if (land.HasTile)
            {
                if ((Main.dayTime || !needDayTime) && (PlowlandSystem.wet.Contains((i, j + 1)) || !needWet))//白天与湿地生长
                {
                    if (Main.rand.Next(100) < (GrowthRate * growMagnification))
                    {
                        if (stage != PlantStage.Grown)
                        {
                            for (int h = 0; h < Height; h++)
                            {
                                Main.tile[i, j - h].TileFrameX += FrameWidth;
                                if (Main.netMode != NetmodeID.SinglePlayer)
                                {
                                    NetMessage.SendTileSquare(-1, i, j - h, 1);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                WorldGen.KillTile(i, j, false, false, true);
            }
        }
        public override void RandomUpdate(int i, int j)
        {
            TryGrow(i, j, HungerSetting.GrowMagnification);
        }
        public PlantStage GetStage(int i, int j)
        {
            Tile tile = Framing.GetTileSafely(i, j);
            return (PlantStage)(tile.TileFrameX / FrameWidth);
        }
        protected virtual void ModifyDropHerbCount(ref int herbItemType, ref int herbItemStack, Player player, PlantStage stage)
        {
            if (stage == PlantStage.Grown)//可采摘的作物不会因为丰收镰刀增加收获
            {
                if (player.HeldItem.type == ModContent.ItemType<丰收镰刀>() && !CanPick) herbItemStack = Main.rand.Next(1, 3);
                else herbItemStack = 1;
            }
        }
        protected virtual void ModifyDropSeedCount(ref int seedItemType, ref int seedItemStack, Player player, PlantStage stage)
        {
            if (!CanPick)//可采摘的作物不会掉落种子
            {
                if (player.HeldItem.type == ItemID.Sickle)
                    seedItemStack = 1;
                else if (player.HeldItem.type == ModContent.ItemType<丰收镰刀>())
                {
                    if (stage == PlantStage.Grown) seedItemStack = Main.rand.Next(1, 3);
                    else seedItemStack = 1;
                }
            }
        }
        public override bool CanDrop(int i, int j)
        {
            Tile tile = Framing.GetTileSafely(i, j);
            PlantStage stage = GetStage(i, j);

            Vector2 worldPosition = new Vector2(i, j).ToWorldCoordinates();
            Player nearestPlayer = Main.player[Player.FindClosest(worldPosition, 16, 16)];

            int herbItemType = HerbItemType;//收获
            int herbItemStack = 0;

            int seedItemType = SeedItemType;//种子
            int seedItemStack = 0;


            if (nearestPlayer.active)
            {
                ModifyDropHerbCount(ref herbItemType, ref herbItemStack, nearestPlayer, stage);
                ModifyDropSeedCount(ref seedItemType, ref seedItemStack, nearestPlayer, stage);
            }
            if (!CropHarvestCantAffect && herbItemStack > 0)//农作物掉落会受到收成影响
            {
                float chance = herbItemStack * nearestPlayer.GetModPlayer<HungerforPlayer>().CropHarvest;
                herbItemStack = (int)chance;
                chance = chance - herbItemStack;
                if (chance > 0 && Main.rand.NextFloat(0, 1) < chance)
                {
                    herbItemStack++;
                }
            }
            var source = new EntitySource_TileBreak(i, j);

            if (herbItemType > 0 && herbItemStack > 0)
            {
                Item.NewItem(source, worldPosition, herbItemType, herbItemStack);
            }

            if (seedItemType > 0 && seedItemStack > 0)
            {
                Item.NewItem(source, worldPosition, seedItemType, seedItemStack);
            }

            return false;
        }
        public override void MouseOver(int i, int j)
        {
            PlantStage stage = GetStage(i, j);
            if (CanPick && stage == PlantStage.Grown)
            {
                Player player = Main.LocalPlayer;
                player.noThrow = 2;
                player.cursorItemIconEnabled = true;
                player.cursorItemIconID = HerbItemType;
            }
        }
        public virtual void TryPick(int i, int j)
        {
            if (GetStage(i, j) != PlantStage.Grown) return;

            //由于贴图高度不确定，采用其他方法寻找顶部瓷砖
            if (Main.tile[i, j].TileFrameY != 0)
            {
                for (int h = 0; h < Height; h++)
                {
                    if (Main.tile[i, j + h].TileType == ModContent.TileType<Arable>())
                    {
                        j += h - Height;
                        break;
                    }
                }
            }

            for (int h = 0; h < Height; h++)
            {
                Main.tile[i, j + h].TileFrameX -= FrameWidth;
                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    NetMessage.SendTileSquare(-1, i, j + h, 1);
                }
            }

            Vector2 worldPosition = new Vector2(i, j).ToWorldCoordinates();
            Player nearestPlayer = Main.player[Player.FindClosest(worldPosition, 16, 16)];

            int herbItemType = HerbItemType;//收获
            int herbItemStack = 1;
            ModifyPick(ref herbItemType, ref herbItemStack);

            if (!CropHarvestCantAffect && herbItemStack > 0)//农作物采摘会受到收成影响
            {
                float chance = herbItemStack * nearestPlayer.GetModPlayer<HungerforPlayer>().CropHarvest;
                herbItemStack = (int)chance;
                chance = chance - herbItemStack;
                if (chance > 0 && Main.rand.NextFloat(0, 1) < chance)
                {
                    herbItemStack++;
                }
            }
            if (ModifyPickDrop(i, j, herbItemStack))
            {
                int item = Item.NewItem(new EntitySource_TileBreak(i, j), worldPosition, herbItemType, herbItemStack);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, item, 1f);
                }
            }
            if (PickJustOneTime)
            {
                nearestPlayer.PickTile(i, j, 1000);
            }
        }
        protected virtual void ModifyPick(ref int herbItemType, ref int herbItemStack) { }
        /// <summary>
        /// 重写采摘掉落，返回false来阻止原先的掉落
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="herbItemStack"></param>
        /// <returns></returns>
        protected virtual bool ModifyPickDrop(int i, int j, int herbItemStack)
        {
            return true;
        }
        public override bool RightClick(int i, int j)
        {
            PlantStage stage = GetStage(i, j);
            if (CanPick && stage == PlantStage.Grown)
            {
                TryPick(i, j);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
