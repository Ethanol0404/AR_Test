# Converted Vessel Meshes

`blood-vessel.obj` was converted offline from:

`C:\Users\xspang\Downloads\done\5\5 export\blood vessel.vtk`

The original file is a legacy binary VTK `POLYDATA` surface exported by 3D Slicer with `SPACE=LPS`.

Classification:

- `blood vessel.vtk`: `POINTS 28778`, `POLYGONS 57296`, `POINT_DATA NORMALS`
- `neoplasm.vtk`: `POINTS 658`, `POLYGONS 1312`, `POINT_DATA NORMALS`

No tube filter was applied because the vessel file already contains polygonal surface data. No arbitrary scale, rotation, handedness conversion, or simplification was applied during conversion. The OBJ preserves the source coordinates and point normals; Unity prefab rebuilds should parent the imported object under `LiverAnatomyPrototype` so it moves, rotates, and scales with the liver model.

To rebuild the Unity prefab after Unity imports the OBJ, run:

`Liver AR/Rebuild Anatomy Prefab From Source Models`
