using Il2CppSynth.SongSelection;
using MelonLoader;
using SRCustomLib;
using SRModCore;
using SRTimestampLib.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SRMultiplayerSongGrabber
{
    /// <summary>
    /// Handles the actual download of a custom map in Unity
    /// </summary>
    public class CustomDownloaderUnity
    {
        private static CustomMapRepoTorrent? _repoTorrent = null;

        private const string apiRootZ = "synthriderz.com/api";
        private const string apiRootSyn = "synplicity.live/api";

        /// <summary>
        /// Downloads the given song to the CustomSongs folder using various fallback methods
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static IEnumerator TryGetSongWithHash(SRLogger logger, string hash, Action onSuccess, Action onFail)
        {
            // Resolve the map's file name up front. The Unity 6 game build broke reading
            // response headers (stripped span helpers), so content-disposition is unusable.
            MapItem? mapInfo = null;
            yield return GetSongInfoFromHash(logger, hash, mapItem => mapInfo = mapItem, null);

            var fileName = mapInfo?.filename;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = hash + ".synth";
                logger.Msg($"No filename from map info; falling back to '{fileName}'");
            }

            string downloadUrlZ = apiRootZ + "/beatmaps/hash/download/" + hash;

            var isSuccess = false;
            void OnSuccessZ()
            {
                isSuccess = true;
                onSuccess?.Invoke();
            }

            void OnFailZ()
            {
                isSuccess = false;
                // Don't trigger fail case yet; fallback still to come
            }

            yield return GetSongWithHash(logger, downloadUrlZ, fileName, OnSuccessZ, OnFailZ);

            // If it worked; we done!
            if (isSuccess)
                yield break;


            // Z download failed; fallback on synplicity
            // Note - could get the song data and use the download url from there, but this is so much simpler
            string downloadUrlSyn = apiRootSyn + "/downloads/" + hash;

            var isSuccessSyn = false;
            void OnSuccessSyn()
            {
                isSuccessSyn = true;
                onSuccess?.Invoke();
            }
            void OnFailSyn()
            {
                isSuccessSyn = false;
                // Don't trigger fail case yet; can still try another fallback
            }

            yield return GetSongWithHash(logger, downloadUrlSyn, fileName, OnSuccessSyn, OnFailSyn);

            // If it worked; we done!
            if (isSuccessSyn)
                yield break;

            // Synplicity download failed; fallback on torrent I guess
            if (string.IsNullOrEmpty(mapInfo?.filename))
            {
                logger.Error("No map info available; cannot fall back to torrent download");
                onFail?.Invoke();
                yield break;
            }

            if (_repoTorrent == null)
            {
                _repoTorrent = new CustomMapRepoTorrent(new SRTimestampLib.SRLogHandler());
                yield return _repoTorrent.Initialize();
            }

            var downloadTask = _repoTorrent.DownloadMapFromFilename(mapInfo.filename);
            while (!downloadTask.IsCompleted)
                yield return null;

            if (downloadTask.IsFaulted || string.IsNullOrEmpty(downloadTask.Result))
            {
                logger.Error($"Failed to download {mapInfo.filename}" +
                    (downloadTask.IsFaulted ? $": {downloadTask.Exception?.GetBaseException().Message}" : ""));
                onFail?.Invoke();
                yield break;
            }

            SongSelectionManager.GetInstance.RefreshSongList(false);
            onSuccess?.Invoke();
        }

        public static IEnumerator GetSongInfoFromHash(SRLogger logger, string hash, Action<MapItem> onSuccess, Action? onFail)
        {
            // Query Z directly (synplicity.live is gone)
            var url = "https://" + apiRootZ + "/beatmaps?s=" + Uri.EscapeDataString("{\"hash\":\"" + hash + "\"}");
            var request = UnityWebRequest.Get(url);

            // Don't hang forever if the site happens to be down
            request.SetTimeoutMsec(5000);

            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                logger.Error("Failed web request: " + request.error);
                onFail?.Invoke();
                yield break;
            }

            // Try to parse
            MapItem? mapItem = null;
            try
            {
                var page = JsonSerializer.Deserialize<BeatmapPageZ>(request.downloadHandler.text, options: new JsonSerializerOptions());
                mapItem = page?.data?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                logger.Error("Failed to parse map info: " + ex.Message);
            }

            if (mapItem == null)
            {
                logger.Error("Failed to parse map info!");
                onFail?.Invoke();
                yield break;
            }

            onSuccess?.Invoke(mapItem);
        }

        /// <summary>
        /// Shape of Z's paginated /api/beatmaps response
        /// </summary>
        public class BeatmapPageZ
        {
            public MapItem[]? data { get; set; }
        }

        /// <summary>
        /// Downloads the given song from the Z site to the CustomSongs folder
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        private static IEnumerator GetSongWithHash(SRLogger logger, string url, string fileName, Action onSuccess, Action onFail)
        {
            var ssmInstance = SongSelectionManager.GetInstance;

            // Download to a temp file
            logger.Msg($"Downloading from '{url}'");
            var songRequest = UnityWebRequest.Get(url);

            // Don't hang forever if Z happens to be down
            songRequest.SetTimeoutMsec(5000);

            //logger.Msg("Data path is " + Application.dataPath);
            string customsPath = Application.dataPath + "/../SynthRidersUC/CustomSongs/";
            // Quest standalone has customs on the SD card
            if (Application.platform == RuntimePlatform.Android)
            {
                customsPath = "/sdcard/SynthRidersUC/CustomSongs";
            }
            logger.Msg("Using customs path " + customsPath);

            // Try to create folder structure if it doesn't exist yet
            Directory.CreateDirectory(customsPath);

            // Unity 6 builds of the game strip DownloadHandlerFile's constructors and
            // break GetResponseHeader, so download to the default buffer and write the
            // file ourselves under the name resolved from the map info.
            yield return songRequest.SendWebRequest();

            if (songRequest.isNetworkError || songRequest.isHttpError)
            {
                logger.Error("GetSong error: " + songRequest.error);
                onFail?.Invoke();
            }
            else
            {
                var destPath = Path.Combine(customsPath, fileName);
                File.WriteAllBytes(destPath, songRequest.downloadHandler.data);
                logger.Msg("Downloaded to " + destPath);

                // Force reload
                ssmInstance.RefreshSongList(false);
                logger.Msg("Updated song list");

                onSuccess?.Invoke();
            }
        }

    }
}
