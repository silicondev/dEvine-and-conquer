﻿using IslandsOfRenguard.Assets.Scripts.World;
using IslandsOfRenguard.Scripts.Player;
using IslandsOfRenguard.Scripts.WorldGen;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IslandsOfRenguard.Scripts.Universal
{
    public class GameSystem : MonoBehaviour
    {
        public GameSystem Instance { get; private set; }

        private PlayerManager _player;
        private Generator _generator;
        private WorldMapperSettings _mapper;

        private List<Chunk> _generatedChunks = new List<Chunk>();
        private List<Chunk> _loadedChunks = new List<Chunk>();
        private Chunk _current;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            Instance = null;
        }

        // Start is called before the first frame update
        void Start()
        {
            //var seed = Random.Range(0.0F, 10000.0F);
            var seed = 1125.624F;
            _mapper = new WorldMapperSettings(80, 100, 105, 200);
            _generator = new Generator(0.01F, seed, 16, new WorldMapper(_mapper));
            Debug.Log("Seed: " + seed.ToString());

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                _player = playerObj.GetComponent<PlayerManager>();
            else
                Debug.LogError("Could not find Player Object");

            StartupGenerate();

            InputEvents input = GetComponent<InputEvents>();
            input.OnMovementKeyPressed += OnMovement;
            input.OnScroll += OnScroll;
        }

        // Update is called once per frame
        void Update()
        {

        }

        /// <summary>
        /// Event calls every time the player moves.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMovement(object sender, EventArgs e)
        {
            _player.OnMove(sender, e);
            RegenChunks();
        }

        private void OnScroll(object sender, EventArgs e)
        {
            _player.OnScroll(sender, e);
            RegenChunks();
        }

        private void StartupGenerate()
        {
            RegenChunks();
        }

        /// <summary>
        /// Either generates or reloads chunks coming into view and unloads chunks going out of view.
        /// </summary>
        private void RegenChunks()
        {
            int posX = (int)_player.transform.position.x;
            int posY = (int)_player.transform.position.y;

            foreach (var chunk in _loadedChunks)
            {
                if (chunk.Contains(posX, posY))
                {
                    _current = chunk;
                    break;
                }
            }

            var topLeftDraw = new Point(posX - _player.ViewDis.X, posY + _player.ViewDis.Y);
            var bottomRightDraw = new Point(posX + _player.ViewDis.X, posY - _player.ViewDis.Y);

            bool hasNewChunks = false;

            List<Chunk> foundChunks = new List<Chunk>();
            for (int y = (int)bottomRightDraw.Y; y < (int)topLeftDraw.Y; y++)
            {
                for (int x = (int)topLeftDraw.X; x < (int)bottomRightDraw.X; x++)
                {
                    // If chunk is already loaded, then skip this loop
                    if (_loadedChunks.GetChunkWithTile(x, y) != null)
                    {
                        foundChunks.Add(_loadedChunks.GetChunkWithTile(x, y));
                        continue;
                    }

                    // Find chunk if exists
                    var id = new Point((int)Math.Floor(x / 16d), (int)Math.Floor(y / 16d));
                    var exists = _generatedChunks.GetChunk(id) != null;
                    var newChunk = exists ? _generatedChunks.GetChunk(id) : new Chunk(id, _generator);

                    // Chunk does not already exist, generate a new one.
                    if (!exists)
                    {
                        hasNewChunks = true;
                        newChunk.Generate();
                        _generatedChunks.Add(newChunk);
                    }

                    // Load chunk
                    _loadedChunks.Add(newChunk);
                    LoadChunk(newChunk);
                    if (newChunk.Contains(posX, posY)) _current = newChunk;
                }
            }
            if (hasNewChunks) Debug.Log("Current Generated Chunks: " + _generatedChunks.Count);

            // Unload unneeded chunks
            List<Chunk> removeChunks = new List<Chunk>();
            foreach (var chunk in _loadedChunks)
            {
                if (foundChunks.GetChunk(chunk.ID) == null)
                {
                    UnloadChunk(chunk);
                    removeChunks.Add(chunk);
                }
            }
            foreach (var chunk in removeChunks)
            {
                if (_loadedChunks.GetChunk(chunk.ID) != null)
                {
                    _loadedChunks.Remove(chunk);
                }
            }
        }

        /// <summary>
        /// Loads the chunk into GameObjects.
        /// </summary>
        /// <param name="chunk">The chunk to load.</param>
        private void LoadChunk(Chunk chunk)
        {
            GameObject grassRef = (GameObject)Instantiate(Resources.Load("Tile_Env_Grass"));
            GameObject stoneRef = (GameObject)Instantiate(Resources.Load("Tile_Env_Stone"));
            GameObject waterRef = (GameObject)Instantiate(Resources.Load("Tile_Env_Water"));
            GameObject sandRef = (GameObject)Instantiate(Resources.Load("Tile_Env_Sand"));

            GameObject treeRef = (GameObject)Instantiate(Resources.Load("Overlay_Env_Tree"));

            for (int y = 0; y < chunk.Tiles.Count; y++)
            {
                //foreach (var tile in tileList)
                for (int x = 0; x < chunk.Tiles[y].Count; x++)
                {
                    var tile = chunk.Tiles[x][y];
                    var overlay = chunk.Overlays[x][y];

                    GameObject obj;
                    GameObject overlayObj = null;
                    var loadOverlay = false;

                    if (tile.ID == TileID.ENV.STONE)
                        obj = Instantiate(stoneRef, transform);
                    else if (tile.ID == TileID.ENV.WATER)
                        obj = Instantiate(waterRef, transform);
                    else if (tile.ID == TileID.ENV.SAND)
                        obj = Instantiate(sandRef, transform);
                    else
                        obj = Instantiate(grassRef, transform);

                    if (overlay.ID == TileID.ENV_OVERLAY.TREE)
                    {
                        loadOverlay = true;
                        overlayObj = Instantiate(treeRef, transform);
                    }

                    obj.name = obj.name.Substring(0, obj.name.Length - 14);
                    if (loadOverlay) overlayObj.name = overlayObj.name.Substring(0, overlayObj.name.Length - 14);

                    obj.transform.position = new Vector3(tile.Location.X + 0.5F, tile.Location.Y + 0.5F, 0);

                    if (loadOverlay)
                        overlayObj.transform.position = new Vector3(overlay.Location.X + 0.5F, overlay.Location.Y + 0.5F, -0.1F);
                    else
                        Destroy(overlayObj);

                    chunk.Objects.Add(obj);
                    chunk.Objects.Add(overlayObj);
                }
            }

            Destroy(grassRef);
            Destroy(stoneRef);
            Destroy(waterRef);
            Destroy(sandRef);

            Destroy(treeRef);
        }

        /// <summary>
        /// Deletes all the user viewable Objects in a chunk.
        /// </summary>
        /// <param name="chunk">The chunk to unload.</param>
        private void UnloadChunk(Chunk chunk)
        {
            foreach (var obj in chunk.Objects)
            {
                Destroy(obj);
            }
            chunk.Objects.Clear();
        }
    }
}
