# work: show or set work priorities, an edit mode command
# work Tasca, work @miners mining 1, work all research 0

if args() = 0
  print dim usage: work <who> [work] [0-4]
  stop
end

# work and a name shows the table, jobs down the side and colonists across
if args() = 1
  total = 0
  colw = 5
  for p in pawns(arg(1))
    total = total + 1
    colw = max(colw, len(p/name) + 2)
  end
  # as many colonists as fit across, then the next ones below
  fit = max(1, floor((cols() - 14) / colw))
  manual = /colony/manualwork
  for start in range(0, total, fit)
    out {pad("", 14)}
    i = 0
    for p in pawns(arg(1))
      if i >= start and i < start + fit
        out bright {pad(p/name, colw)}
      end
      i = i + 1
    end
    print
    for w in /work
      job = name(w)
      out dim {pad(job, 14)}
      i = 0
      for p in pawns(arg(1))
        if i >= start and i < start + fit
          if not exists(p/work)
            out dim {pad("-", colw)}
          else
            v = p/work/{job}
            if v = "disabled"
              out dim {pad("-", colw)}
            else if v = 0
              out dim {pad("·", colw)}
            else if manual = no
              out {pad("✓", colw)}
            else if v = 1
              out bright {pad(v, colw)}
            else
              out {pad(v, colw)}
            end
          end
        end
        i = i + 1
      end
      print
    end
  end
  stop
end

# work, a name and a job sets it
if not exists("/work/" + arg(2))
  print bad no work called {arg(2)}
  stop
end
job = name(path("/work/" + arg(2)))
n = arg(3)
if args() < 3 or n < 0 or n > 4
  print bad give a priority from 0 (off) to 4
  stop
end
if /colony/manualwork = no and n > 0
  print dim manual priorities are off in the work tab, so it just turns it on
end

changed = ""
for p in pawns(arg(1))
  if not exists(p/work)
    print warn {p/name} doesn't work
  else
    try set p/work/{job} = n
    if error = none
      if changed != ""
        changed = changed + ", "
      end
      changed = changed + p/name
    end
  end
end

if changed != ""
  if n = 0
    print good {changed}: {job} off
  else if /colony/manualwork = no
    print good {changed}: {job} on
  else
    print good {changed}: {job} {n}
  end
end
