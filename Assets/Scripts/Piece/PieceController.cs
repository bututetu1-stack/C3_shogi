using UnityEngine;
using System.Collections;

public class PieceController : MonoBehaviour
{
    private PieceInstance pieceInstance;
    private PieceRenderer pieceRenderer;
    // 揺れ演出の基準位置（移動・スライドで更新される）
    private Vector3 homePos;
    private bool isShaking;

    public void Init(PieceInstance piece)
    {
        pieceInstance = piece;
        pieceRenderer = GetComponent<PieceRenderer>();
        homePos = transform.position;
    }

    public PieceInstance GetPiece()
    {
        return pieceInstance;
    }

    public PieceRenderer GetRenderer()
    {
        return pieceRenderer;
    }

    public void MoveTo(Vector2Int newPos)
    {
        pieceInstance.boardPosition = newPos;
        homePos = new Vector3(newPos.x, newPos.y, 0);
        transform.position = homePos;
    }

    public IEnumerator SlideToCoroutine(Vector2Int newPos, float duration)
    {
        Vector3 startPos = homePos;
        Vector3 endPos = new Vector3(newPos.x, newPos.y, 0);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            homePos = Vector3.Lerp(startPos, endPos, t);
            if (!isShaking) transform.position = homePos;
            yield return null;
        }
        homePos = endPos;
        if (!isShaking) transform.position = homePos;
    }

    public void UpdateHP()
    {
        if (pieceRenderer != null)
            pieceRenderer.UpdateHP();
    }

    public void Shake()
    {
        if (!isShaking)
            StartCoroutine(ShakeCoroutine(0.3f, 0.06f));
    }

    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        isShaking = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            Vector3 offset = new Vector3(Random.Range(-magnitude, magnitude), Random.Range(-magnitude, magnitude), 0f);
            transform.position = homePos + offset;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = homePos;
        isShaking = false;
    }

    public void DestroyPiece()
    {
        pieceInstance.isAlive = false;
        Destroy(gameObject);
    }
}
