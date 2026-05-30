// TileType.cs
// 지구 산책 레이어(Pikmin 타일맵) 타일 종류.
// byte 백킹 — 청크당 32×32 = 1024칸을 byte[] 한 장으로 직렬화하기 위함.
// 값은 직렬화/네트워크 호환을 위해 고정한다(재배치 금지, 추가만).
public enum TileType : byte
{
    Grass = 0,
    Path = 1,
    Water = 2,
    Forest = 3,
    // Building = 4,  // v2
}
