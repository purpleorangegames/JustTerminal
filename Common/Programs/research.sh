# research: what's being researched, in edit mode it picks the next one
# research, research electricity, research microelectronics_basics

if args() = 0
  print bright researching {/research/current}
  print {round(/research/progress)}% done, {/research/done} of {/research/total} projects finished
  print dim can start: {/research/available}
  stop
end

set /research/current = rest(1)
print good researching {/research/current}
