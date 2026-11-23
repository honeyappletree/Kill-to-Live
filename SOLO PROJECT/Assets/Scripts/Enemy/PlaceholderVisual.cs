using System.Collections.Generic;
using UnityEngine;

public enum PlaceholderShape { Circle, Triangle }

/// <summary>
/// 정식 아트가 나오기 전까지 쓰는 도형 placeholder 시각 표현.
/// finalSprite가 비어 있으면 shape/placeholderColor로 도형을 procedural 생성해 SpriteRenderer에 적용하고,
/// finalSprite가 채워지면 도형 생성을 건너뛰고 그 스프라이트를 그대로 사용한다.
/// → 나중에 정식 아트가 나오면 이 필드에 스프라이트만 끌어넣으면 코드 수정 없이 반영됨.
///
/// MaggotColony/Maggot_Split 전용 컴포넌트. 기존 3종 몬스터(Rotten_Zombie/Skeleton_Hound/Crawler)는
/// SpriteRenderer 직접 할당 + Animator 프레임 애니메이션 방식을 그대로 쓰며 이 컴포넌트와 무관하다.
///
/// placeholderColor는 최종 아트 방향이 아니라 임시 식별용 색상이다.
/// </summary>
public class PlaceholderVisual : MonoBehaviour
{
    const int TextureSize = 64;

    [Header("정식 아트 (연결되면 아래 도형 설정을 무시하고 이 스프라이트를 그대로 사용)")]
    [SerializeField] private Sprite finalSprite;

    [Header("도형 Placeholder (finalSprite가 비어 있을 때만 사용)")]
    [SerializeField] private PlaceholderShape shape = PlaceholderShape.Circle;
    [SerializeField] private Color placeholderColor = Color.white;

    static readonly Dictionary<(PlaceholderShape, Color32), Sprite> spriteCache = new();

    void Awake()
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        sr.sprite = finalSprite != null ? finalSprite : GetOrCreatePlaceholderSprite(shape, placeholderColor);
    }

    static Sprite GetOrCreatePlaceholderSprite(PlaceholderShape shape, Color color)
    {
        var key = (shape, (Color32)color);
        if (spriteCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var sprite = GenerateShapeSprite(shape, color);
        spriteCache[key] = sprite;
        return sprite;
    }

    static Sprite GenerateShapeSprite(PlaceholderShape shape, Color color)
    {
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[TextureSize * TextureSize];
        Color32 opaque = color;
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                bool inside = shape == PlaceholderShape.Circle ? IsInsideCircle(x, y) : IsInsideTriangle(x, y);
                pixels[y * TextureSize + x] = inside ? opaque : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
    }

    static bool IsInsideCircle(int x, int y)
    {
        float radius = TextureSize * 0.5f - 1f;
        float dx = x + 0.5f - TextureSize * 0.5f;
        float dy = y + 0.5f - TextureSize * 0.5f;
        return dx * dx + dy * dy <= radius * radius;
    }

    static bool IsInsideTriangle(int x, int y)
    {
        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
        Vector2 a = new Vector2(TextureSize * 0.5f, TextureSize * 0.95f);
        Vector2 b = new Vector2(TextureSize * 0.05f, TextureSize * 0.05f);
        Vector2 c = new Vector2(TextureSize * 0.95f, TextureSize * 0.05f);

        float d1 = Cross(p, a, b);
        float d2 = Cross(p, b, c);
        float d3 = Cross(p, c, a);

        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    static float Cross(Vector2 p1, Vector2 p2, Vector2 p3) =>
        (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
}
