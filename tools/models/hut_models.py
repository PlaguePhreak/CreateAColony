"""
Writes the simple placeholder models of the hut blocks (and the Supply Camp and Field marker) to
assets/createacolony/shapes/block/. Each hut is a crate with a few boxes on top that show what the hut is for.
The Town Hall and Farmer's Hut models were made by hand by Aaron's friend and are not written here.

Usage: python3 tools/models/hut_models.py [output folder]
"""
import json, sys
def box(name, f, t, tex, tex_top=None):
    x1,y1,z1=f; x2,y2,z2=t
    w,h,d=x2-x1,y2-y1,z2-z1
    def uv(a,b): return [0,0,round(a,2),round(b,2)]
    faces={
      "north":{"texture":"#"+tex,"uv":uv(w,h)},
      "east":{"texture":"#"+tex,"uv":uv(d,h)},
      "south":{"texture":"#"+tex,"uv":uv(w,h)},
      "west":{"texture":"#"+tex,"uv":uv(d,h)},
      "up":{"texture":"#"+(tex_top or tex),"uv":uv(w,d)},
      "down":{"texture":"#"+tex,"uv":uv(w,d)},
    }
    return {"name":name,"from":f,"to":t,"faces":faces}
def shape(textures, elements):
    return {"textureWidth":16,"textureHeight":16,"textures":{k:k for k in textures},"elements":elements}
base=[box("Crate",[1,0,1],[15,8,15],"sides"),box("Lid",[0,8,0],[16,9,16],"planks")]
smelter=shape(["sides","planks","stone","coal","metal"], base+[
  box("Furnace",[4,9,4],[12,14,12],"stone"),
  box("Coals",[5,14,5],[11,14.5,11],"coal"),
  box("Ingot1",[1,9,11.5],[4,10,13.5],"metal"),
  box("Ingot2",[1,10,12],[3.5,10.8,13.2],"metal"),
])
smith=shape(["sides","planks","metal","stone"], base+[
  box("Block",[5,9,5],[11,10.5,11],"stone"),
  box("Waist",[6.5,10.5,6.5],[9.5,12,9.5],"metal"),
  box("Face",[3,12,6],[12,14,10],"metal"),
  box("Horn",[12,12.5,7],[14.5,13.6,9],"metal"),
])
B=lambda *a: box(*a)
models = {
 # Builder: a toolbox with a hammer and a plank leaning on it.
 "hut-builder": (["sides","planks","metal","top"], [
   B("Box",[3,9,5],[13,12,11],"planks","top"),
   B("BoxHandle",[7,12,7.5],[9,13.5,8.5],"planks"),
   B("HammerHandle",[4,12,4],[12,12.8,4.8],"planks"),
   B("HammerHead",[10.5,11.6,3.4],[12.5,13.6,5.4],"metal"),
   B("Plank",[1.5,9,11.5],[14.5,10,14],"planks"),
 ]),
 # Cook: a pot on a little fire.
 "hut-cook": (["sides","planks","stone","coal","metal","cloth"], [
   B("Ring",[4,9,4],[12,10,12],"stone"),
   B("Coals",[5,10,5],[11,10.5,11],"coal"),
   B("Pot",[5.5,10.5,5.5],[10.5,14,10.5],"metal"),
   B("Rim",[5,14,5],[11,14.5,11],"metal"),
   B("Cloth",[1,9,11.5],[4,9.6,14.5],"cloth"),
 ]),
 # Forester: a stump with an axe in it.
 "hut-forester": (["sides","planks","log","top","metal"], [
   B("Stump",[4,9,4],[12,14,12],"log","top"),
   B("AxeHandle",[7.5,14,7.5],[8.5,20,8.5],"planks"),
   B("AxeHead",[8.5,17.5,7.6],[11,19.5,8.4],"metal"),
 ]),
 # Guard Tower: a little lookout tower with a blue roof.
 "hut-guardtower": (["sides","planks","log","cloth"], [
   B("Leg1",[4,9,4],[5,15,5],"log"), B("Leg2",[11,9,4],[12,15,5],"log"),
   B("Leg3",[4,9,11],[5,15,12],"log"), B("Leg4",[11,9,11],[12,15,12],"log"),
   B("Floor",[3.5,15,3.5],[12.5,16,12.5],"planks"),
   B("Roof",[3,18,3],[13,19,13],"cloth"),
   B("Post",[4,16,4],[5,18,5],"log"), B("Post2",[11,16,11],[12,18,12],"log"),
 ]),
 # Miner: a pickaxe and a pile of ore.
 "hut-miner": (["sides","planks","stone","coal","metal"], [
   B("Rock1",[2,9,3],[8,12,9],"stone"),
   B("Rock2",[4,12,4],[7,13.5,7],"stone"),
   B("Ore",[8,9,8],[11,11,11],"coal"),
   B("PickHandle",[9,9.1,2],[10,10.1,14],"planks"),
   B("PickHead",[6,9.1,11],[13,10.6,12],"metal"),
 ]),
 # Potter: three pots.
 "hut-potter": (["sides","planks","pot","top"], [
   B("Pot1",[2.5,9,2.5],[7.5,14,7.5],"pot"), B("Neck1",[3.5,14,3.5],[6.5,15,6.5],"pot"),
   B("Pot2",[9,9,3],[13,12,7],"pot"),
   B("Pot3",[5,9,9],[11,13,13],"pot"), B("Neck3",[6,13,10],[10,14,12],"pot"),
 ]),
 # Prospector: a rock with a propick leaning on it, and a map.
 "hut-prospector": (["sides","planks","stone","metal","cloth"], [
   B("Rock",[3,9,3],[9,13,9],"stone"),
   B("PickHandle",[9.5,9,5],[10.5,19,6],"planks"),
   B("PickHead",[7.5,18,5],[13.5,19,6],"metal"),
   B("Map",[2,9,10],[12,9.4,14],"cloth"),
 ]),
 # Quarry: stacked stone blocks and a pick.
 "hut-quarry": (["sides","planks","stone","metal"], [
   B("Stone1",[2,9,2],[8,13,8],"stone"), B("Stone2",[8.5,9,2],[14,13,8],"stone"),
   B("Stone3",[4,13,3],[10,17,8],"stone"),
   B("PickHandle",[3,9.1,10],[14,10.1,11],"planks"),
   B("PickHead",[12,9.1,8.5],[13,10.6,13.5],"metal"),
 ]),
 # Residence: a little bed.
 "hut-residence": (["sides","planks","cloth","linen"], [
   B("Frame",[2,9,4],[14,11,12],"planks"),
   B("Blanket",[2.2,11,4.2],[10,12,11.8],"cloth"),
   B("Pillow",[10.5,11,5],[13.5,12.5,11],"linen"),
   B("Head",[13.5,9,3.5],[14.5,14,12.5],"planks"),
 ]),
 # Tavern: a barrel and two mugs.
 "hut-tavern": (["sides","planks","metal","top"], [
   B("Barrel",[2,9,3],[10,17,11],"planks","top"),
   B("Hoop1",[1.8,10.5,2.8],[10.2,11.3,11.2],"metal"), B("Hoop2",[1.8,14.7,2.8],[10.2,15.5,11.2],"metal"),
   B("Mug1",[11,9,4],[13.5,12,6.5],"planks","top"), B("Mug2",[11,9,9],[13.5,12,11.5],"planks","top"),
 ]),
 # Warehouse: stacked crates.
 "hut-warehouse": (["sides","planks","top"], [
   B("Crate1",[2,9,2],[8,15,8],"sides","top"), B("Crate2",[8.5,9,3],[14,14,8.5],"sides","top"),
   B("Crate3",[3,15,3],[7.5,19,7.5],"sides","top"), B("Sack",[4,9,9.5],[12,13,14],"planks"),
 ]),
}

# Supply camp: a crate with a small tent beside it (no lid crate base).
camp=shape(["sides","planks","cloth","top"], [
  B("Crate",[1,0,8],[8,7,15],"sides","top"),
  B("TentL",[8,0,1],[9,10,15],"cloth"), B("TentR",[15,0,1],[16,10,15],"cloth"),
  B("TentTop",[8,10,1],[16,11,15],"cloth"),
  B("Pole",[11.5,0,0.5],[12.5,12,1.5],"planks"),
  B("Sack",[2,0,2],[7,5,6.5],"planks"),
])

# Field marker: a scarecrow two blocks tall.
crow=shape(["planks","cloth","hay","linen"], [
  B("Post",[7,0,7],[9,28,9],"planks"),
  B("Arms",[1,19,7.25],[15,20.5,8.75],"planks"),
  B("Body",[4.5,14,6],[11.5,23,10],"cloth"),
  B("SleeveL",[1.5,18.5,6.75],[4.5,21,9.25],"cloth"),
  B("SleeveR",[11.5,18.5,6.75],[14.5,21,9.25],"cloth"),
  B("Head",[5.5,23,5.5],[10.5,28,10.5],"linen"),
  B("HatBrim",[4,28,4],[12,28.6,12],"hay"),
  B("HatTop",[5.5,28.6,5.5],[10.5,31,10.5],"hay"),
  B("Straw",[5,12.5,6.5],[11,14,9.5],"hay"),
])
saw=shape(["sides","planks","bark","metal"], base+[
  box("Horse1",[3,9,5],[4.5,12,11],"planks"),
  box("Horse2",[11.5,9,5],[13,12,11],"planks"),
  box("Log",[1,12,6],[15,15,9],"bark"),
  box("Blade",[7.5,12.5,4],[8,16,11.5],"metal"),
  box("Handle",[7.25,15,11.5],[8.25,16,13.5],"planks"),
])
el=base+[
  box("RackBase",[3,9,6],[13,10,10],"planks"),
  box("RackPostL",[3,10,7.5],[4,15,8.5],"planks"),
  box("RackPostR",[12,10,7.5],[13,15,8.5],"planks"),
  box("RackBar",[3,14,7.5],[13,15,8.5],"planks"),
]
for i,x in enumerate([5.5,7.75,10]):
  el.append(box(f"Spear{i}",[x,10,7.6],[x+0.8,16,8.4],"planks"))
  el.append(box(f"Tip{i}",[x-0.1,16,7.5],[x+0.9,17.5,8.5],"metal"))
el.append(box("Flag",[3.2,15,8.5],[3.8,18,13],"cloth"))
barracks=shape(["sides","planks","metal","cloth"], el)

if __name__ == "__main__":
    import os
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "../../assets/createacolony/shapes/block")
    for name, (tex, el) in models.items():
        json.dump(shape(tex, base + el), open(f"{out}/{name}.json", "w"), indent=1)
    for name, model in {"hut-smelter": smelter, "hut-blacksmith": smith, "hut-sawmill": saw, "hut-barracks": barracks, "supplycamp": camp, "fieldmarker": crow}.items():
        json.dump(model, open(f"{out}/{name}.json", "w"), indent=1)
