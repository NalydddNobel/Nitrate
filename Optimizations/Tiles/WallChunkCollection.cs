﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nitrate.API.Tiles;
using Nitrate.Utilities;
using System;
using Terraria;

namespace Nitrate.Optimizations.Tiles;

internal sealed class WallChunkCollection : ChunkCollection
{
    public override bool ApplyOverride => false;

    public override void PopulateChunk(Point key)
    {
        Chunk chunk = Loaded[key];
        RenderTarget2D target = chunk.RenderTarget;

        chunk.AnimatedPoints.Clear();

        GraphicsDevice device = Main.graphics.GraphicsDevice;

        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);

        Vector2 chunkPositionWorld = new(key.X * ChunkSystem.CHUNK_SIZE, key.Y * ChunkSystem.CHUNK_SIZE);

        const int size_tiles = ChunkSystem.CHUNK_SIZE / 16;

        Point chunkPositionTile = new((int)chunkPositionWorld.X / 16, (int)chunkPositionWorld.Y / 16);

        Main.tileBatch.Begin();

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        for (int i = -1; i < size_tiles + 1; i++)
        {
            for (int j = -1; j < size_tiles + 1; j++)
            {
                int tileX = chunkPositionTile.X + i;
                int tileY = chunkPositionTile.Y + j;

                if (!WorldGen.InWorld(tileX, tileY))
                {
                    continue;
                }

                Tile tile = Framing.GetTileSafely(tileX, tileY);

                if (AnimatedTileRegistry.IsWallPossiblyAnimated(tile.WallType))
                {
                    chunk.AnimatedPoints.Add(new AnimatedPoint(tileX, tileY, AnimatedPointType.AnimatedTile));
                }
                else
                {
                    // Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(tileX * 16 - (int)chunkPositionWorld.X, tileY * 16 - (int)chunkPositionWorld.Y, 16, 16), Color.Yellow);
                    ModifiedTileDrawing.DrawSingleWall(false, tileX, tileY, chunkPositionWorld);
                }
            }
        }

        Main.tileBatch.End();
        Main.spriteBatch.End();

        device.SetRenderTargets(null);
    }

    public override void DrawChunksToChunkTarget(GraphicsDevice device)
    {
        if (ScreenTarget is null)
        {
            return;
        }

        RenderTargetBinding[] bindings = device.GetRenderTargets();

        foreach (RenderTargetBinding binding in bindings)
        {
            ((RenderTarget2D)binding.RenderTarget).RenderTargetUsage = RenderTargetUsage.PreserveContents;
        }

        device.SetRenderTarget(ScreenTarget);
        device.Clear(Color.Transparent);

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );

        FnaVector2 screenPosition = Main.screenPosition;

        Rectangle screenArea = new((int)screenPosition.X, (int)screenPosition.Y, Main.screenWidth, Main.screenHeight);

        foreach (Point key in Loaded.Keys)
        {
            Chunk chunk = Loaded[key];
            RenderTarget2D target = chunk.RenderTarget;

            Rectangle chunkArea = new(key.X * ChunkSystem.CHUNK_SIZE, key.Y * ChunkSystem.CHUNK_SIZE, target.Width, target.Height);

            if (!chunkArea.Intersects(screenArea))
            {
                continue;
            }

            // This should never happen, something catastrophic happened if it did.
            // The check here is because rendering disposed targets generally has strange behaviour and doesn't always throw exceptions.
            // Therefore this check needs to be made as it's more robust.
            if (target.IsDisposed)
            {
                throw new Exception("Attempted to render a disposed chunk.");
            }

            Main.spriteBatch.Draw(target, new Vector2(chunkArea.X, chunkArea.Y) - screenPosition, Color.White);
        }

        Main.spriteBatch.End();

        device.SetRenderTargets(bindings);
    }

    public void DoRenderWalls(GraphicsDevice graphicsDevice, RenderTarget2D? screenSizeLightingBuffer, RenderTarget2D? screenSizeOverrideBuffer, Lazy<Effect> lightMapRenderer, SpriteBatchUtil.SpriteBatchSnapshot? snapshot)
    {
        DrawChunksToChunkTarget(graphicsDevice);
        RenderChunksWithLighting(screenSizeLightingBuffer, screenSizeOverrideBuffer, lightMapRenderer);

        if (snapshot.HasValue)
        {
            Main.tileBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
            Main.spriteBatch.BeginWithSnapshot(snapshot.Value);
        }

        foreach (Point key in Loaded.Keys)
        {
            Chunk chunk = Loaded[key];

            foreach (AnimatedPoint wallPoint in chunk.AnimatedPoints)
            {
                if (wallPoint.Type != AnimatedPointType.AnimatedTile)
                {
                    continue;
                }

                // ModifiedWallDrawing.DrawSingleWallMostlyUnmodified(wallPoint.X, wallPoint.Y, new Vector2(key.X * ChunkSystem.CHUNK_SIZE, key.Y * ChunkSystem.CHUNK_SIZE));
                // Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(wallPoint.X * 16 - (int)Main.screenPosition.X, wallPoint.Y * 16 - (int)Main.screenPosition.Y, 16, 16), Color.Red);
                ModifiedTileDrawing.DrawSingleWall(true, wallPoint.X, wallPoint.Y, Main.screenPosition);
            }
        }

        if (snapshot.HasValue)
        {
            Main.tileBatch.End();
            Main.spriteBatch.End();
            Main.spriteBatch.BeginWithSnapshot(snapshot.Value);
        }
    }
}