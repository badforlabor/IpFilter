using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;

namespace IpFilter
{
    class Program
    {
        static uint AddressToInt(string v)
        {
            // 检查而已
            if (v.Split('.').Length != 4)
                Console.WriteLine("AddressToInt failed. v=" + v);


            uint i = v.Split('.')
                      .Select(uint.Parse)
                      .Aggregate((a, b) => a * 256 + b);

            return i;
        }

        // 二分查找某个区间段
        static int BinarySearch(ref int cnt, List<uint> ips, uint ip)
        {
            // 不用递归实现
            int begin = 0;
            int end = ips.Count;
            cnt = ips.Count / 2;
            int find_idx = -1;
            do
            {
                int mid = (begin + end) / 2;

                if (begin >= end)
                    break;

                // 偶数位为ip起始值，奇数位为ip终止值，所以如下
                mid = mid - mid % 2;
                if (ip >= ips[mid] && ip <= ips[mid + 1])
                {
                    find_idx = mid;
                    break;
                }

                // 如果发现是最后一组，那么终止
                if (begin == mid)
                    break;
                
                if (ip < ips[mid + 1])
                    end = mid;
                else if (ip > ips[mid])
                    begin = mid;
                else
                    break;

                cnt--;
            }
            while (cnt >= 0);

            cnt = ips.Count / 2 - cnt;

            return find_idx;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("inet_aton(127.0.0.1)={0}", AddressToInt("127.0.0.1"));

            /*
             功能参考：
             * http://ftp.apnic.net/apnic/stats/apnic/delegated-apnic-latest
             * https://www.apnic.net/about-apnic/corporate-documents/documents/resource-guidelines/rir-statistics-exchange-format/
             * https://www.oschina.net/code/snippet_177666_33250
             */

            StringBuilder buffer = new StringBuilder();

            // 构建字典，偶数位存储ip起始值，+1位存储的是ip终止值
            List<uint> IpBegins = new List<uint>();

            // 读取APNIC文件
            string[] apnic_array = File.ReadAllLines("apnic.txt");
            foreach (var apnic in apnic_array)
            {
                if (apnic.StartsWith("#"))
                    continue;

                try
                {
                    // 格式是：
                    // registry|cc|type|start|value|date|status[|extensions...]
                    string[] segs = apnic.Split('|');
                    if (segs.Length == 0)
                        continue;

                    // 过滤掉非CN的
                    if (segs[1] != "CN")
                        continue;

                    // 暂时只支持ipv4
                    if (segs[2] != "ipv4")
                        continue;

                    //                     // status这个字段有别的赋值吗？
                    //                     if (segs[6] != "allocated")
                    //                         Console.WriteLine("amazing value, segs[6]={0}, in {1}", segs[6], apnic);

                    // ip start, ip end
                    uint ipStart = AddressToInt(segs[3]);
                    uint ipCount = Convert.ToUInt32(segs[4]);
                    uint ipEnd = ipStart + ipCount - 1;

                    if (ipCount == 0)
                        continue;

                    buffer.Append(string.Format("{0},{1},{2}\r\n", segs[3], ipStart.ToString(), ipCount.ToString()));

                    // 插入到IpBegins中

                    int idx = -1;
                    for (int i = 0; i < IpBegins.Count - 1; i += 2)
                    {
                        // 找到一个合法的区间段
                        if (ipStart >= IpBegins[i] && (i + 2 >= IpBegins.Count || ipStart < IpBegins[i + 2]))
                        {
                            idx = i;
                            break;
                        }
                    }
                    if (idx == -1)
                    {
                        idx = IpBegins.Count;
                    }
                    else
                    {
                        if (ipStart >= IpBegins[idx] && ipStart <= IpBegins[idx + 1])
                        {
                            //说明有穿插，那么截掉旧数据，不过，这里应该不会发生
                            uint tmp = IpBegins[idx + 1];
                            IpBegins[idx + 1] = ipStart;
                            ipStart = tmp;
                        }
                        // 排在这个后面
                        idx += 2;
                    }
                    IpBegins.Insert(idx, ipStart);
                    IpBegins.Insert(idx + 1, ipEnd);

                }
                catch (Exception)
                {
                    Console.WriteLine("exception on line {0}", apnic);
                }
            }

            File.WriteAllText("parsed.txt", buffer.ToString());

            // 测试某个ip
            {
                //string ip = "64.233.189.113";
                string ip = "119.109.5.46";
                uint iip = AddressToInt(ip);
                int cnt = 0;
                int bfind = BinarySearch(ref cnt, IpBegins, iip);
                Console.WriteLine("查找ip[{0}]，结果为：[{1}]，执行步骤：{2}", ip, bfind == -1 ? "没找到" : "找到了", cnt);
            }
            // 测试所有中国ip
            if(false)
            {
                for (int i = 2; i < IpBegins.Count - 1; i+=2)
                {
                    for (int j = (int)(IpBegins[i + 1] - IpBegins[i]); j >= 0; j--)
                    {
                        uint iip = IpBegins[i] + (uint)j;
                        int cnt = 0;
                        int bfind = BinarySearch(ref cnt, IpBegins, iip);
                        if (bfind == -1)
                            Console.WriteLine("查找ip[{0}]，结果为：[{1}]，执行步骤：{2}", iip, bfind == -1 ? "没找到" : "找到了", cnt);
                    }
                }
            }

            Console.WriteLine("处理完成，按任意键关闭...");
            Console.ReadKey();
        }
    }
}
