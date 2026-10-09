using UnityEngine;
using System.Collections;

public class PieceController : MonoBehaviour
{
    private PieceInstance pieceInstance;
    private PieceRenderer pieceRenderer;
    private Vector3 originalPos;
    private bool isShaking;

    public void Init(PieceInstance piece)
    {
        pieceInstance = piece;
        pieceRenderer = GetComponent<PieceRenderer>();
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
        transform.position = new Vector3(newPos.x, newPos.y, 0);
    }

    public IEnumerator SlideToCoroutine(Vector2Int newPos, float duration)
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = new Vector3(newPos.x, newPos.y, 0);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        transform.position = endPos;
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
        originalPos = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = originalPos.x + Random.Range(-magnitude, magnitude);
            float y = originalPos.y + Random.Range(-magnitude, magnitude);
            transform.position = new Vector3(x, y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPos;
        isShaking = false;
    }

    public void DestroyPiece()
    {
        pieceInstance.isAlive = false;
        Destroy(gameObject);
    }
}
