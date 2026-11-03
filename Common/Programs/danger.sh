# danger: enemies and wild predators, closest to a colonist first
# /danger/list has enemies first, then predators, animals of the same kind together

if /danger/count = 0
  print good no danger on the map
  stop
end

shown = none
for d in /danger/list
  # a title before the first enemy and before the first predator
  if d/enemy = yes and shown = none
    print bad ENEMIES {/danger/enemies}
    shown = "enemies"
  else if d/enemy = no and shown != "predators"
    if shown = "enemies"
      print
    end
    print warn PREDATORS {/danger/predators}
    shown = "predators"
  end

  if d/enemy = yes
    out bad   {pad(d/name, 18)}
  else
    out warn   {pad(d/name, 18)}
  end
  out dim {pad(d/faction, 22)}
  meter d/health 6
  if d/distance != none and d/distance < 20
    out bad   {d/distance} cells away
  else if d/distance != none
    out dim   {d/distance} cells away
  end
  if d/state != none
    out dim   {d/state}
  end
  print
end
