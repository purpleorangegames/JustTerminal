# research: what's being researched, in edit mode it picks the next one
# research, research electricity, research microelectronics_basics
# with JustDesktop there's a queue: research <project> queues what it needs first,
# research then <project> adds to the end, research clear empties it

if args() = 0
  print bright researching {/research/current}
  print {round(/research/progress)}% done, {/research/done} of {/research/total} projects finished
  if exists(/research/queue)
    if /research/queue != "empty"
      print dim queue, with when each is done:
      print {/research/queue}
    end
  end
  print dim can start: {/research/available}
  stop
end

if lower(arg(1)) = "then" or lower(arg(1)) = "clear"
  if not exists(/research/queue)
    print bad the research queue comes with JustDesktop
    stop
  end
  if lower(arg(1)) = "clear"
    set /research/queue = "clear"
    print good the queue is empty
    stop
  end
  set /research/queue = rest(2)
  print good queued, the queue is now:
  print {/research/queue}
  stop
end

set /research/current = rest(1)
print good researching {/research/current}
if exists(/research/queue)
  if /research/queue != "empty"
    print dim queue, with when each is done:
    print {/research/queue}
  end
end
