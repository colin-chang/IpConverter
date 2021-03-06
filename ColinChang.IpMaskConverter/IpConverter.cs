﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace ColinChang.IpMaskConverter
{
    /// <summary>
    /// IP地址转换器
    /// </summary>
    public static class IpConverter
    {
        /// <summary>
        /// 将完整IP转为10进制无符号整形
        /// </summary>
        /// <returns>The number.</returns>
        /// <param name="ip">待转换完整IP地址</param>
        public static uint ToIpNumber(this string ip)
        {
            if (!IPAddress.TryParse(ip, out var addr))
                throw new ArgumentException($"{ip} 不是有效IP地址");

            var ips = addr.ToString().Split('.').Select(uint.Parse).ToList();
            return ips[0] << 0x18 | ips[1] << 0x10 | ips[2] << 0x8 | ips[3];
        }

        /// <summary>
        /// 将10进制无符号整形转为完整IP地址
        /// </summary>
        /// <returns></returns>
        /// <param name="ip">待转换10进制无符号整形</param>
        public static string ToIpAddress(this uint ip) =>
            $"{ip >> 0x18 & 0xff}.{ip >> 0x10 & 0xff}.{ip >> 0x8 & 0xff}.{ip & 0xff}";


        /// <summary>
        /// 将IP地址段转为 IP地址+掩码的形式,如：192.168.0.0-192.168.0.255 -> 192.168.0.0/24
        /// </summary>
        /// <returns>The ip and mask.</returns>
        /// <param name="startIp">起始IP</param>
        /// <param name="endIp">结束IP</param>
        public static IEnumerable<string> ToIpAndMask(string startIp, string endIp) =>
            ToIpAndMask(startIp.ToIpNumber(), endIp.ToIpNumber());


        /// <summary>
        /// 将IP地址段转为 IP地址+掩码的形式,如：192.168.0.0-192.168.0.255 -> 192.168.0.0/24
        /// </summary>
        /// <returns>The ip and mask.</returns>
        /// <param name="startIp">起始IP 10进制形式</param>
        /// <param name="endIp">结束IP 10进制形式</param>
        public static IEnumerable<string> ToIpAndMask(uint startIp, uint endIp)
        {
            /*
            * 0、处理起始IP低位不是0和截止IP低位不是1的情况   和起止IP相同的情况
            * 1、找出起始IP中低位连续是0的位数，找出截止IP中低位连续是1的位数，取较小者记作n，掩码为32-n
            * 2、起始和截止IP分别 >>n 后做差+1，记作m 即需要切割几段,
            * 3、循环m次，从起始IP开始，掩码始终为32-n，每次IP递增2的N次方
            */

            var result = new List<string>();
            if (startIp == endIp)
            {
                result.Add($"{startIp.ToIpAddress()}/32");
                return result;
            }

            // 起始IP最后一位是1的置为0，截止IP最后一位是254的置为255
            if ((startIp & 0xff) == 1)
                startIp--;
            if ((endIp & 0xff) == 254)
                endIp++;


            //起始IP 右起0 位数
            var scnt = 1;
            while (startIp >> scnt << scnt == startIp)
                scnt++;
            scnt--;
            //if (scnt <= 0)
            //throw new Exception($"起始IP{startIp}不合法");

            //截止IP 右起1 位数
            var eReverse = ~endIp;
            var ecnt = 1;
            while (eReverse >> ecnt << ecnt == eReverse)
                ecnt++;
            ecnt--;
            //if (ecnt <= 0)
            //throw new Exception($"截止IP{endIp}不合法");


            var cnt = scnt < ecnt ? scnt : ecnt;

            if (cnt <= 0)
            {
                for (var i = startIp; i <= endIp; i++)
                    result.Add($"{i.ToIpAddress()}/32");
            }
            else
            {
                var mask = 32 - cnt;
                var periods = (endIp >> cnt) - (startIp >> cnt) + 1;
                for (var i = 0; i < periods; i++)
                {
                    result.Add($"{startIp.ToIpAddress()}/{mask}");
                    startIp += (uint) (1 << cnt);
                }
            }

            return result;
        }

        /// <summary>
        /// 将IP地址+掩码 转为IP地址段，如 192.168.0.0/24 -> 192.168.0.0-192.168.0.255
        /// </summary>
        /// <returns>The ip period.</returns>
        /// <param name="ipAndMask">Ip and mask.</param>
        public static (string StartIp, string EndIp) ToIpPeriod(this string ipAndMask)
        {
            if (string.IsNullOrWhiteSpace(ipAndMask))
                throw new Exception("IP地址和掩码为空");

            var parts = ipAndMask.Split('/');
            if (parts.Length != 2)
                throw new Exception("IP地址和掩码格式非法");

            if (!IPAddress.TryParse(parts[0], out var addr))
                throw new Exception("IP地址非法");

            if (!uint.TryParse(parts[1], out var mask) || mask > 32)
                throw new Exception("掩码非法");

            var start = addr.ToString();
            var end = start.ToIpNumber() + (1 << (int) (32 - mask)) - 1;
            return (start, ((uint) end).ToIpAddress());
        }

        /// <summary>
        /// 将IP地址+掩码 转为IP地址列表，如 192.168.0.0/31 -> 192.168.0.0,192.168.0.1
        /// </summary>
        /// <returns>The ip list.</returns>
        /// <param name="ipAndMask">Ip and mask.</param>
        public static List<string> ToIpList(this string ipAndMask)
        {
            var (startIp, endIp) = ipAndMask.ToIpPeriod();
            var ips = new List<string>();
            uint start = startIp.ToIpNumber(),
                end = endIp.ToIpNumber();
            for (var i = start; i <= end; i++)
                ips.Add(i.ToIpAddress());

            return ips;
        }
    }
}