using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirtyThree : MonoBehaviour
{
    public void Probability()
    {
        int n = 6; // 初始概率1/6

        while (n >= 1)
        {
            int r = Random.Range(1, n + 1); // 生成1~n的随机数（包含n）
            if (r == 1)
            {
                Debug.Log("中奖！概率为 1/" + n);
                break;
            }
            else
            {
                Debug.Log("未中，概率为 1/" + n);
                n--;
            }
        }
    }
}
