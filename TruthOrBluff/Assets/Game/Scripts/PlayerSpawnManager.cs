using UnityEngine;
using System.Collections.Generic;

namespace LiarsBar
{
    /// <summary>
    /// 玩家生成管理器：负责在圆形桌子周围动态生成玩家
    /// </summary>
    public class PlayerSpawnManager : MonoBehaviour
    {
        [Header("桌子配置")]
        public Transform TableCenter; // 桌子中心点
        public float TableRadius = 3f; // 玩家距离桌子中心的距离
        public float PlayerHeight = 0f; // 玩家生成高度（相对桌面）
        
        [Header("角色预制体")]
        public GameObject[] CharacterPrefabs; // 可选的角色模型
        public GameObject PlayerControllerPrefab; // PlayerController预制体模板
        
        [Header("玩家父节点")]
        public Transform PlayersContainer; // 所有玩家的父对象
        
        [Header("调试")]
        public bool ShowGizmos = true;
        public Color GizmosColor = Color.yellow;
        
        private List<PlayerController> spawnedPlayers = new List<PlayerController>();

        void Awake()
        {
            if (TableCenter == null)
                TableCenter = transform;
                
            if (PlayersContainer == null)
            {
                var container = new GameObject("Players");
                PlayersContainer = container.transform;
                PlayersContainer.SetParent(transform);
            }
        }

        /// <summary>生成所有玩家</summary>
        public List<PlayerController> SpawnPlayers(PlayerData[] playersData)
        {
            // 清除之前的玩家
            ClearPlayers();
            
            int playerCount = playersData.Length;
            if (playerCount == 0)
            {
                Debug.LogWarning("玩家数量为0，无法生成");
                return spawnedPlayers;
            }
            
            Debug.Log($"开始生成 {playerCount} 名玩家，围绕桌子半径 {TableRadius}m");
            
            for (int i = 0; i < playerCount; i++)
            {
                // 计算位置（均匀分布在圆周上）
                Vector3 position = GetPlayerPosition(i, playerCount);
                Quaternion rotation = GetPlayerRotation(i, playerCount);
                
                // 实例化 PlayerController
                GameObject playerObj = Instantiate(PlayerControllerPrefab, position, rotation, PlayersContainer);
                playerObj.name = $"Player_{i}_{playersData[i].Name}";
                
                var controller = playerObj.GetComponent<PlayerController>();
                if (controller == null)
                {
                    Debug.LogError($"PlayerControllerPrefab 缺少 PlayerController 组件！");
                    Destroy(playerObj);
                    continue;
                }
                
                // 选择角色模型（循环使用）
                GameObject characterPrefab = null;
                if (CharacterPrefabs != null && CharacterPrefabs.Length > 0)
                {
                    int characterIndex = i % CharacterPrefabs.Length;
                    characterPrefab = CharacterPrefabs[characterIndex];
                }
                
                // 初始化玩家
                controller.Initialize(playersData[i], characterPrefab);
                spawnedPlayers.Add(controller);
                
                Debug.Log($"生成玩家 {i}: {playersData[i].Name} 在位置 {position}");
            }
            
            return spawnedPlayers;
        }

        /// <summary>计算玩家位置（圆形排列）</summary>
        Vector3 GetPlayerPosition(int playerIndex, int totalPlayers)
        {
            // 计算角度（从0度开始，顺时针分布）
            float angleStep = 360f / totalPlayers;
            float angle = playerIndex * angleStep;
            
            // 转换为弧度
            float angleRad = angle * Mathf.Deg2Rad;
            
            // 计算位置（使用极坐标）
            Vector3 centerPos = TableCenter.position;
            float x = centerPos.x + TableRadius * Mathf.Sin(angleRad);
            float z = centerPos.z + TableRadius * Mathf.Cos(angleRad);
            float y = centerPos.y + PlayerHeight;
            
            return new Vector3(x, y, z);
        }

        /// <summary>计算玩家旋转（朝向桌子中心）</summary>
        Quaternion GetPlayerRotation(int playerIndex, int totalPlayers)
        {
            Vector3 playerPos = GetPlayerPosition(playerIndex, totalPlayers);
            Vector3 lookDirection = TableCenter.position - playerPos;
            lookDirection.y = 0; // 保持水平朝向
            
            if (lookDirection != Vector3.zero)
                return Quaternion.LookRotation(lookDirection);
            else
                return Quaternion.identity;
        }

        /// <summary>清除所有已生成的玩家</summary>
        public void ClearPlayers()
        {
            foreach (var player in spawnedPlayers)
            {
                if (player != null)
                    Destroy(player.gameObject);
            }
            spawnedPlayers.Clear();
            
            Debug.Log("已清除所有玩家");
        }

        /// <summary>获取指定索引的玩家控制器</summary>
        public PlayerController GetPlayer(int index)
        {
            if (index >= 0 && index < spawnedPlayers.Count)
                return spawnedPlayers[index];
            return null;
        }

        /// <summary>获取所有玩家控制器</summary>
        public List<PlayerController> GetAllPlayers()
        {
            return spawnedPlayers;
        }

        // ========== 编辑器辅助 ==========

        void OnDrawGizmos()
        {
            if (!ShowGizmos || TableCenter == null)
                return;
            
            Gizmos.color = GizmosColor;
            
            // 绘制桌子圆形范围
            DrawCircle(TableCenter.position, TableRadius, 32);
            
            // 如果在编辑模式，显示预览位置
            if (!Application.isPlaying)
            {
                int previewPlayerCount = 4; // 默认预览4个玩家位置
                for (int i = 0; i < previewPlayerCount; i++)
                {
                    Vector3 pos = GetPlayerPosition(i, previewPlayerCount);
                    Gizmos.DrawWireSphere(pos, 0.3f);
                    
                    // 绘制朝向箭头
                    Vector3 direction = (TableCenter.position - pos).normalized;
                    Gizmos.DrawLine(pos, pos + direction * 0.5f);
                }
            }
        }

        void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(0, 0, radius);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(radius * Mathf.Sin(angle), 0, radius * Mathf.Cos(angle));
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

        void OnValidate()
        {
            // 确保半径为正数
            TableRadius = Mathf.Max(0.5f, TableRadius);
        }
    }
}
