﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.IO;
using System.Linq;
using System.Timers;
using System.ComponentModel;
using Nativa;

namespace WebServer
{
    class FileCache
    {
        //简单的缓存机制
        //一个不是很好的寿命机制

        private readonly ConcurrentDictionary<string, byte[]> cache = new ConcurrentDictionary<string, byte[]>();
        private readonly ConcurrentDictionary<string, int> lifeDict = new ConcurrentDictionary<string, int>();
        private readonly Timer timer;
        private readonly Logger logger;
        private readonly FileCacheSettings settings;

        private void ExtendLife(string filename)
        {
            if (!lifeDict.ContainsKey(filename)) lifeDict.TryAdd(filename, settings.initLife);
            else if (lifeDict[filename] < settings.firstGenLifeMax) lifeDict[filename] += settings.firstGenLifeGrowth;
            else if (lifeDict[filename] < settings.secondGenLifeMax) lifeDict[filename] += settings.secondGenLifeGrowth;
            else if (lifeDict[filename] < settings.thirdGenLifeMax) lifeDict[filename] += settings.thirdGenLifeGrowth;
        }

        public byte[] ReadFile(string filename)
        {
            ExtendLife(filename);
            if (!cache.ContainsKey(filename)) cache.TryAdd(filename, File.ReadAllBytes(filename));
            return cache[filename];
        }

        public byte[] ReadTextFile(string filename) //暂且重新编码为 UTF-8，权宜之计，以后再改。
        {
            ExtendLife(filename);
            if (!cache.ContainsKey(filename)) cache.TryAdd(filename, Encoding.UTF8.GetBytes(File.ReadAllText(filename)));
            return cache[filename];
        }

        public FileCache(Logger logger, FileCacheSettings settings)
        {
            timer = new Timer(settings.cacheClearingInterval);
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
            this.logger = logger;
            this.settings = settings;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var killList = new List<string>();
            foreach (var pair in lifeDict)
            {
                logger.Log(string.Format("寿命 {0} {1}", pair.Key, pair.Value));
                if (pair.Value == 0)
                {
                    killList.Add(pair.Key);
                }
                else
                {
                    --lifeDict[pair.Key];
                }
            }
            killList.ForEach(
                file =>
                {
                    cache.Remove(file, out _);
                    lifeDict.Remove(file, out _);
                    logger.Log(string.Format("清理文件缓存 {0}", file));
                });
            GC.Collect();
        }
    }
}
