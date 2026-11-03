# checkup: diseases with how the fight against them goes, and the wounds already treated

n = 0
for p in /health/list
  if count(p/sick) + count(p/chronic) + count(p/tended) > 0
    n = n + 1
    print bright {p/name}

    # diseases: how sick, and how much immunity against it
    for h in p/sick
      out   • {pad(h/name, 18)}
      out dim {"sick "}
      if h/severity > 60
        bar (h/severity * 100 / h/max) 10 bad
      else
        bar (h/severity * 100 / h/max) 10 warn
      end
      if h/immunity = none
        # no immunity to build, like artery blockage
        out dim {" " + padl(round(h/severity) + "%", 4) + "  "}
        out warn no immunity, gets worse
      else
        out dim {" " + padl(round(h/severity) + "%", 4) + "  immune "}
        bar h/immunity 10 good
        out dim {" " + padl(round(h/immunity) + "%", 4) + "  "}
        if h/immunity >= h/severity
          out good winning
        else
          out bad losing
        end
      end
      if h/tend = "needs tending"
        out warn   {h/tend}
      else if h/tend != none
        out dim   {h/tend}
      end
      print
    end

    # illnesses that stay, like bad back
    for h in p/chronic
      part = ""
      if h/part != none
        part = " · " + h/part
      end
      out warn   • {h/name}{part}
      if h/tend = "needs tending"
        out warn   {h/tend}
      else if h/tend != none
        out dim   {h/tend}
      end
      print
    end

    # wounds a doctor already treated
    for h in p/tended
      part = ""
      if h/part != none
        part = " · " + h/part
      end
      out   • {h/name}{part}
      if h/tend != none
        out dim   {h/tend}
      end
      print
    end
  end
end

if n = 0
  print good nobody is sick or being treated
end
