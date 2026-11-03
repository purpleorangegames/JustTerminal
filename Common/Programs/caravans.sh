# caravans: where they are going, when they get there and their food

if count(/caravans) = 0
  print dim no caravans
  stop
end
for c in /caravans
  out bright {pad(c/name, 20)}
  out {pad(c/pawns + " pawns", 10)}
  if c/moving = yes
    out {pad("to " + c/destination, 24)}
    out dim {pad(c/arrives, 14)}
  else
    out dim {pad("resting", 38)}
  end
  if c/food = "plenty"
    print good food plenty
  else if c/food < 3
    print bad food {c/food} days
  else
    print food {c/food} days
  end
end
