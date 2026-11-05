# 坦克贴图源文件

`gen_tank_art.py` 生成 `Assets/game_obj/tank/坦克机身.png` 和 `炮塔.png`，同时可以输出可编辑的 SVG 源文件。

## 生成

```bash
python gen_tank_art.py --out out          # PNG + SVG
python gen_tank_art.py --out out --format png
python gen_tank_art.py --out out --format svg
```

依赖 Pillow：`pip install pillow`。

## 贴回 Unity

- 用 `out/hull.png` 覆盖 `Assets/game_obj/tank/坦克机身.png`
- 用 `out/turret.png` 覆盖 `Assets/game_obj/tank/炮塔.png`
- 保留同名 `.meta`（GUID 不能变），贴图 Max Size 为 1024

## 画布约定（改图前先看）

- 画布 1024×1024，Unity 里 PPU 100；车身缩放 0.2，炮塔缩放 (0.25, 0.2)
- 车身图**车头朝下**，`tank.prefab` 里旋转 180°
- 炮塔图**炮管朝左**，`tank.prefab` 里旋转 -90°；画布中心就是炮塔旋转轴，所以炮塔圆心必须画在 (512, 512)
- 炮塔圆画成 `rx = ry * 0.8` 的椭圆，经过炮塔的非均匀缩放后在游戏里才是正圆
- 车身可见轮廓对应 1.3×1.5 的碰撞体；炮口到车身中心约 1.18 世界单位，子弹在 1.2 处生成

## SVG

`hull.svg` / `turret.svg` 是同一组形状的矢量版本，方便在 Inkscape / Figma 里改。

注意：SVG 的描边以路径为中心，脚本生成 PNG 时描边是向内画的，两者外轮廓会差半个描边宽。需要替换游戏贴图时以脚本重新生成的 PNG 为准。

## 配色

取自 `Assets/control/style/TankTheme.uss`；琥珀 `#f2b545` 与 UI 主按钮同色。
