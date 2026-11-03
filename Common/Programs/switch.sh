# switch: things with a power switch, in edit mode it turns them on or off
# switch, switch 3 off, switch heater off for every heater
# a colonist goes and flicks it, like with the game's button

if args() < 2
  for s in /power/switches
    out bright {pad(name(s), 4)}
    out {pad(s/name, 24)}
    out dim {pad(s/room, 14)}
    print {s/on}
  end
  stop
end
n = 0
for s in /power/switches
  if name(s) + 0 = arg(1) + 0 or lower(s/name) has lower(replace(arg(1), "_", " "))
    set s/on = arg(2)
    n = n + 1
  end
end
if n = 0
  print bad no switch {arg(1)}, switch lists them
  stop
end
print good {n} set to {arg(2)}
