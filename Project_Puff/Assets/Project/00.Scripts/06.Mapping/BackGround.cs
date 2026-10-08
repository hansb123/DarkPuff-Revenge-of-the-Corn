using UnityEngine;

public class BackGround : MonoBehaviour
{
    [SerializeField] Transform cam;                     
    [Range(0f, 1f)]
    [SerializeField] float parallax = 0.5f;              
    [SerializeField] bool followY = false;               // 세로로도 따라갈지, 말지

    Transform[] tiles;
    float width;
    float total;
    float lastCamX, lastCamY;

    void Start()
    {
        if (cam == null) cam = Camera.main.transform;

        tiles = new Transform[transform.childCount];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = transform.GetChild(i);

        width = tiles[0].GetComponent<SpriteRenderer>().bounds.size.x;
        total = width * tiles.Length;

        lastCamX = cam.position.x;
        lastCamY = cam.position.y;
    }

    void LateUpdate()
    {
        //패럴랙스
        float dx = cam.position.x - lastCamX;
        float dy = cam.position.y - lastCamY;
        transform.position += new Vector3(dx * parallax, followY ? dy * parallax : 0f, 0f);
        lastCamX = cam.position.x;
        lastCamY = cam.position.y;

        // 무한 반복
        float half = total * 0.5f;
        foreach (var t in tiles)
        {
            while (t.position.x < cam.position.x - half) t.position += Vector3.right * total;
            while (t.position.x > cam.position.x + half) t.position += Vector3.left * total;
        }
    }
}