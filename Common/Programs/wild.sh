# wild: the wild animals on the map, closest to the colony first
# hunt and tame mark them, wild | grep boar finds the boars

if count(/wild) = 0
  print dim no wild animals
  stop
end
for a in /wild
  out bright {pad(name(a), 4)}
  out {pad(a/kind, 16)}
  out dim {pad(a/pos, 10)}
  if a/manhunter
    out bad manhunter 
  end
  if a/hunt = "yes"
    out warn hunt 
  end
  if a/tame = "yes"
    out good tame 
  end
  print
end
